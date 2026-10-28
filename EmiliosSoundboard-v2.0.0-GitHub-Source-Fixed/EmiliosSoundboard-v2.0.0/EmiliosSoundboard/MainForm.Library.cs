using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;


namespace EmiliosSoundboard
{
    /// <summary>Sound library: cards, search, sorting, import, edit, delete.</summary>
    internal sealed partial class MainForm : Form
    {
        // ------------------------------------------------------------------ cards

        private void AddCardFor(SoundItem item, int delayIndex)
        {
            var card = new Card(item);
            card.Appear = -0.07f * delayIndex;
            cards.Add(card);
            viewReady = false;
            forceFrame = true;
        }

        private void CreateSearchUi()
        {
            searchTitle = new Label { Text = "SOUND LIBRARY", ForeColor = Color.FromArgb(132, 220, 255), BackColor = Color.Transparent, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), AutoSize = false, TextAlign = ContentAlignment.MiddleLeft };
            Controls.Add(searchTitle);
            searchBox = new TextBox
            {
                Font = new Font("Segoe UI", 10f, FontStyle.Regular),
                BackColor = Color.FromArgb(10, 17, 31),
                ForeColor = Color.FromArgb(240, 246, 255),
                BorderStyle = BorderStyle.None,
                PlaceholderText = "Find a sound or file type...",
                Dock = DockStyle.Fill
            };
            searchBox.TextChanged += (s, e) => { scrollTarget = 0; LayoutCards(); forceFrame = true; Invalidate(); };
            searchBoxFrame = new AccentSearchFrame { Accent = Theme.Blue, BackColor = Color.Transparent, Padding = new Padding((int)(11 * scale), (int)(4 * scale), (int)(8 * scale), (int)(4 * scale)) };
            searchBoxFrame.Controls.Add(searchBox);
            Controls.Add(searchBoxFrame);

            searchTags = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                BackColor = Color.Transparent,
                Padding = new Padding((int)(4 * scale)),
                Margin = Padding.Empty,
                AutoScroll = false
            };
            string[] tags = { "ALL", "NAME A-Z", "LENGTH", "TYPE" };
            int sortButtonWidth = (int)(88f * scale);
            foreach (string tag in tags)
            {
                var chip = new SearchSortChip
                {
                    Text = tag,
                    Tag = tag,
                    Size = new Size(sortButtonWidth, (int)(30 * scale)),
                    Margin = new Padding((int)(3 * scale)),
                    Accent = Theme.Blue,
                    Selected = String.Equals(tag, sortMode, StringComparison.OrdinalIgnoreCase),
                    Font = new Font("Segoe UI", 8.2f, FontStyle.Bold)
                };
                chip.Click += (s, e) => { sortMode = Convert.ToString(((Control)s).Tag); RefreshSearchTheme(); LayoutCards(); Invalidate(); };
                searchTags.Controls.Add(chip);
            }
            Controls.Add(searchTags);
            Resize += (s, e) => PositionSearchUi();
            PositionSearchUi();
            RefreshSearchTheme();
        }

        private void PositionSearchUi()
        {
            if (searchBox == null || searchTags == null || IsDisposed) return;
            int width = (int)(LW >= 850f ? 204f * scale : 176f * scale);
            int x = (int)(36f * scale);
            int logicalY = (int)Math.Max(132f, Math.Min(LH * 0.34f, LH - 220f));
            int y = (int)(logicalY * scale);
            bool showSidebar = LW >= 850f && sidebarOpen;
            searchTitle.Visible = showSidebar;
            searchBoxFrame.Visible = showSidebar;
            searchTags.Visible = showSidebar;
            searchTitle.SetBounds(x, y - (int)(24f * scale), width, (int)(22f * scale));
            searchBoxFrame.SetBounds(x, y, width, (int)(38f * scale));
            searchTags.SetBounds(x - (int)(2f * scale), y + (int)(44f * scale), width + (int)(4f * scale), (int)(78f * scale));
            searchGlassRect = new RectangleF(20f, logicalY - 42f, width / scale + 24f, 166f);
            if (sidebarArrow != null)
            {
                sidebarArrow.SetBounds(0, y + (int)(40f * scale), (int)(28f * scale), (int)(44f * scale));
                sidebarArrow.BringToFront();
            }
            searchTitle.BringToFront(); searchBoxFrame.BringToFront(); searchTags.BringToFront();
            if (sidebarArrow != null) sidebarArrow.BringToFront();
            Invalidate();
        }

        private bool MatchesSearch(SoundItem item)
        {
            if (item == null) return false;
            string q = searchBox == null ? "" : (searchBox.Text ?? "").Trim();
            if (!String.IsNullOrEmpty(q) && (item.Name ?? "").IndexOf(q, StringComparison.OrdinalIgnoreCase) < 0 &&
                Path.GetExtension(item.AudioFile ?? "").IndexOf(q, StringComparison.OrdinalIgnoreCase) < 0) return false;
            return true;
        }

        private int VisibleCount()
        {
            if (!viewReady) RebuildView();
            return view.Count;
        }

        /// <summary>
        /// Duration of the audio file in ticks, cached per file name. Opening a reader is far too
        /// expensive to do inside a sort comparison, which is what the LENGTH sort used to do
        /// on every frame.
        /// </summary>
        private long AudioFileBytes(SoundItem item)
        {
            string name = item.AudioFile ?? "";
            long cached;
            if (durationCache.TryGetValue(name, out cached)) return cached;

            long ticks;
            try
            {
                string path = Path.Combine(store.SoundsDir, name);
                using (var reader = new NAudio.Wave.AudioFileReader(path)) ticks = reader.TotalTime.Ticks;
            }
            catch
            {
                try { ticks = new FileInfo(Path.Combine(store.SoundsDir, name)).Length; }
                catch { ticks = 0L; }
            }

            durationCache[name] = ticks;
            return ticks;
        }

        /// <summary>The visible, sorted card list for the current frame. Never allocate per call.</summary>
        private IEnumerable<Card> OrderedVisibleCards()
        {
            if (!viewReady) RebuildView();
            return view;
        }

        /// <summary>
        /// Rebuilds the visible list. Called once per frame from the layout pass instead of the
        /// three LINQ queries plus sort that used to run for every paint and every tick.
        /// </summary>
        private void RebuildView()
        {
            view.Clear();
            for (int i = 0; i < cards.Count; i++)
            {
                Card c = cards[i];
                if (!c.Removing && MatchesSearch(c.Item)) view.Add(c);
            }

            if (sortMode == "NAME A-Z")
                view.Sort((a, b) => string.Compare(a.Item.Name, b.Item.Name, StringComparison.OrdinalIgnoreCase));
            else if (sortMode == "LENGTH")
                view.Sort((a, b) => AudioFileBytes(a.Item).CompareTo(AudioFileBytes(b.Item)));
            else if (sortMode == "TYPE")
                view.Sort((a, b) => string.Compare(Path.GetExtension(a.Item.AudioFile ?? ""),
                                                  Path.GetExtension(b.Item.AudioFile ?? ""),
                                                  StringComparison.OrdinalIgnoreCase));

            viewReady = true;
        }

        private void LayoutCards()
        {
            RebuildView();
            const float margin = 36f, gap = 20f, minW = 250f, maxW = 400f;
            float avail = Math.Max(200f, LW - margin);
            int cols = Math.Max(1, (int)Math.Floor((avail + gap) / (minW + gap)));
            float cw = Math.Min(maxW, (avail - gap * (cols - 1)) / cols);

            int live = VisibleCount();
            // Avoid tiny cards on ultrawide/full-screen windows when only a few sounds exist.
            // The grid should adapt to the number of cards currently present, not reserve
            // empty columns for sounds that have not been added yet.
            if (live > 0 && live < cols)
            {
                cols = live;
                cw = Math.Min(maxW, (avail - gap * (cols - 1)) / cols);
            }
            int usedCols = Math.Min(cols, Math.Max(1, live));
            float gridW = usedCols * cw + (usedCols - 1) * gap;
            float x0 = (LW - gridW) / 2f;

            int idx = 0;
            foreach (Card c in OrderedVisibleCards())
            {
                c.TargetW = cw;
                int col = idx % cols;
                int row = idx / cols;
                c.TargetX = x0 + col * (cw + gap);
                c.TargetY = 14f + row * (CardH + gap);
                idx++;
            }

            int rows = live == 0 ? 0 : (live + cols - 1) / cols;
            float contentH = live == 0 ? 0f : 14f + rows * (CardH + gap) - gap + 24f;
            float viewH = Math.Max(0f, GridBottom - GridTop);
            maxScroll = Math.Max(0f, contentH - viewH);
            scrollTarget = Math.Max(0f, Math.Min(maxScroll, scrollTarget));
        }

        private void SaveLibrary()
        {
            if (!store.Save())
                ShowToast("Could not save your library: " + store.LastSaveError, Theme.Danger);
        }

        // ------------------------------------------------------------------ adding sounds

        private static string CleanName(string raw)
        {
            string s = (raw ?? "").Replace('_', ' ').Trim();
            return s.Length == 0 ? "New sound" : s;
        }

        private async void AddSounds()
        {
            if (importing) return;
            importing = true;
            try
            {
                string[] files;
                using (var ofd = new OpenFileDialog())
                {
                    ofd.Title = "Choose one or more sound files";
                    ofd.Filter = "Audio files|*.wav;*.mp3;*.aiff;*.aif;*.m4a;*.aac;*.wma;*.flac;*.mp4|All files (*.*)|*.*";
                    ofd.Multiselect = true;
                    ofd.CheckFileExists = true;
                    if (ofd.ShowDialog(this) != DialogResult.OK) return;
                    files = ofd.FileNames;
                }

                var problems = new List<string>();
                int added = 0;
                string lastName = "";

                for (int i = 0; i < files.Length; i++)
                {
                    string file = files[i];
                    string shortName = Path.GetFileName(file);

                    string probeError = null;
                    bool ok = await Task.Run(() =>
                    {
                        TimeSpan d;
                        string er;
                        bool r = AudioPlayer.TryProbe(file, out d, out er);
                        probeError = er;
                        return r;
                    });

                    if (!ok)
                    {
                        problems.Add(shortName + "\n    " + probeError);
                        continue;
                    }

                    string sub = files.Length > 1
                        ? "File " + (i + 1) + " of " + files.Length + "  -  " + shortName
                        : shortName;
                    string defaultName = store.UniqueName(CleanName(Path.GetFileNameWithoutExtension(file)), null);

                    string name, imageSource, device;
                    using (var dlg = new SoundDetailsDialog(store, "NAME YOUR SOUND", sub, defaultName, null,
                                                            files.Length > 1, "ADD SOUND", null))
                    {
                        DialogResult r = dlg.ShowDialog(this);
                        if (r == DialogResult.Cancel) break;
                        if (r == DialogResult.Ignore) continue;
                        name = store.UniqueName(dlg.SoundName, null);
                        imageSource = dlg.ChosenImagePath;
                        device = dlg.OutputDevice;
                    }

                    ImportResult res;
                    try
                    {
                        res = await Task.Run(() => store.ImportNew(file, name, imageSource));
                    }
                    catch (Exception ex)
                    {
                        problems.Add(shortName + "\n    Could not copy the file: " + ex.Message);
                        Log.Warn("import", "could not copy \"" + file + "\"", ex);
                        continue;
                    }

                    res.Item.OutputDevice = device;
                    store.Data.Sounds.Add(res.Item);
                    AddCardFor(res.Item, 0);
                    added++;
                    lastName = res.Item.Name;
                    if (res.Warning != null)
                    {
                        problems.Add(shortName + "\n    " + res.Warning);
                        Log.Warn("import", shortName + ": " + res.Warning);
                    }
                    else
                    {
                        Log.Info("import", "added \"" + res.Item.Name + "\" from " + shortName +
                                           (string.IsNullOrEmpty(device) ? "" : " (device: " + device + ")"));
                    }
                }

                if (added > 0)
                {
                    SaveLibrary();
                    scrollTarget = float.MaxValue;
                    ShowToast(added == 1 ? "Added \"" + lastName + "\"" : "Added " + added + " sounds", Theme.Success);
                }

                if (problems.Count > 0)
                {
                    string text = string.Join("\n\n", problems.Take(6));
                    if (problems.Count > 6) text += "\n\n...and " + (problems.Count - 6) + " more.";
                    MessageDialog.Show(this, "Some files could not be added", text);
                }
            }
            catch (Exception ex)
            {
                MessageDialog.Show(this, "Could not add the sound", ex.Message);
            }
            finally
            {
                importing = false;
                forceFrame = true;
            }
        }

        // ------------------------------------------------------------------ editing and deleting

        private void ShowCardMenu(Card c, Point clientPoint)
        {
            var menu = DarkMenu.Create();
            menu.Items.Add("Edit name and background...", null, (s, e) => EditCard(c));
            menu.Items.Add("Change background image...", null, (s, e) => ChangeImage(c));

            var revert = new ToolStripMenuItem("Use default background");
            revert.Enabled = !string.IsNullOrEmpty(c.Item.ImageFile);
            revert.Click += (s, e) => RevertImage(c);
            menu.Items.Add(revert);

            menu.Items.Add(new ToolStripSeparator());
            var del = new ToolStripMenuItem("Delete sound...") { Tag = "danger" };
            del.Click += (s, e) => DeleteCard(c);
            menu.Items.Add(del);

            menu.Closed += (s, e) => BeginInvoke(new Action(menu.Dispose));
            menu.Show(this, clientPoint);
        }

        private async void EditCard(Card c)
        {
            if (c.Removing) return;
            var item = c.Item;
            try
            {
                using (var dlg = new SoundDetailsDialog(store, "EDIT SOUND",
                    "Change the name, the background or the playback device of this button.", item.Name,
                    store.ImagePath(item), false, "SAVE", item, item.OutputDevice))
                {
                    if (dlg.ShowDialog(this) != DialogResult.OK) return;

                    item.Name = store.UniqueName(dlg.SoundName, item);
                    item.OutputDevice = dlg.OutputDevice;
                    if (dlg.ChosenImagePath != null) await ApplyImage(c, dlg.ChosenImagePath);
                    else if (dlg.RevertImage) DropImage(c);

                    if (player.CurrentId == item.Id) currentName = item.Name;
                    SaveLibrary();
                    ShowToast("Saved \"" + item.Name + "\"", Theme.Success);
                }
            }
            catch (Exception ex)
            {
                MessageDialog.Show(this, "Could not save the changes", ex.Message);
            }
        }

        private async void ChangeImage(Card c)
        {
            if (c.Removing) return;
            string file;
            using (var ofd = new OpenFileDialog())
            {
                ofd.Title = "Choose a background image for \"" + c.Item.Name + "\"";
                ofd.Filter = "Images (*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif)|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff|All files (*.*)|*.*";
                ofd.CheckFileExists = true;
                if (ofd.ShowDialog(this) != DialogResult.OK) return;
                file = ofd.FileName;
            }

            if (await ApplyImage(c, file))
            {
                SaveLibrary();
                ShowToast("Background updated", Theme.Success);
            }
        }

        private async Task<bool> ApplyImage(Card c, string source)
        {
            string newName;
            try
            {
                newName = await Task.Run(() => store.ImportImage(source));
            }
            catch (Exception ex)
            {
                MessageDialog.Show(this, "Could not use that image",
                    "Please choose a PNG, JPG or BMP picture.\n\n" + ex.Message);
                return false;
            }

            string old = c.Item.ImageFile;
            c.Item.ImageFile = newName;
            if (!string.IsNullOrEmpty(old)) store.DeleteImageFile(old);
            c.DropCover();
            forceFrame = true;
            return true;
        }

        private void DropImage(Card c)
        {
            string old = c.Item.ImageFile;
            c.Item.ImageFile = "";
            if (!string.IsNullOrEmpty(old)) store.DeleteImageFile(old);
            c.DropCover();
            forceFrame = true;
        }

        private void RevertImage(Card c)
        {
            if (c.Removing || string.IsNullOrEmpty(c.Item.ImageFile)) return;
            DropImage(c);
            SaveLibrary();
            ShowToast("Background reset to default", Theme.Success);
        }

        private void DeleteCard(Card c)
        {
            if (c.Removing) return;

            bool confirmed = MessageDialog.Show(this, "Delete this sound?",
                "\"" + c.Item.Name + "\" will be removed from your soundboard, together with the copy of its audio " +
                "and background that this app stored.\n\nYour original files on your computer are not touched.",
                "DELETE", "KEEP IT", true);
            if (!confirmed) return;

            if (player.CurrentId == c.Item.Id)
            {
                player.Stop();
                localMonitorPlayer.Stop();
                state = PlayState.Ready;
                lastPos = TimeSpan.Zero;
                lastDur = TimeSpan.Zero;
            }

            string name = c.Item.Name;
            c.Removing = true;
            viewReady = false;
            if (c.Appear > 1f) c.Appear = 1f;
            bool clean = store.RemoveSound(c.Item);
            SaveLibrary();

            if (clean) ShowToast("Deleted \"" + name + "\"", Theme.Muted);
            else ShowToast("Deleted \"" + name + "\", but a stored file could not be removed.", Color.FromArgb(255, 190, 100));
            forceFrame = true;
        }
    }
}
