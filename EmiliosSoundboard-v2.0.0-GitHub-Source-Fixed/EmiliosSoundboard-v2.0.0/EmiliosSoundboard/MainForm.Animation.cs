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
    /// <summary>The 60 Hz game loop: easing, space field, desktop pet.</summary>
    internal sealed partial class MainForm : Form
    {
        // ------------------------------------------------------------------ animation loop

        private static float Ease(float cur, float target, float rate, float dt, ref bool busy)
        {
            float v = Theme.Smooth(cur, target, rate, dt);
            if (Math.Abs(target - v) < 0.004f) return target;
            busy = true;
            return v;
        }

        private void OnCoolMusicEnded(object sender, PlaybackEndedEventArgs e)
        {
            if (!IsDisposed && store.Data.Settings.CoolMode)
            {
                string musicPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "RainbowRoad.mp3");
                if (File.Exists(musicPath)) { try { coolMusic.Play("cool-mode-music", musicPath, store.Data.Settings.OutputDevice); } catch (Exception ex) { Log.Warn("audio", "cool mode music could not start", ex); } }
            }
        }

        private void OnTick(object sender, EventArgs e)
        {
            if (WindowState == FormWindowState.Minimized || !Visible || ClientSize.Width < 50)
            {
                clock.Restart();
                return;
            }

            float dt = (float)Math.Min(0.05, clock.Elapsed.TotalSeconds);
            clock.Restart();
            time += dt;
            StepDesktopPet(dt);
            if (store.Data.Settings.DesktopPetEnabled && desktopPetSprite != null) forceFrame = true;

            bool busy = Step(dt);
            if (busy || forceFrame)
            {
                forceFrame = false;
                Invalidate();
            }
        }

        private float PetRandom(float min, float max)
        {
            return min + (float)petRandom.NextDouble() * (max - min);
        }

        private void StepDesktopPet(float dt)
        {
            var settings = store.Data.Settings;
            string path = settings.DesktopPetImagePath ?? "";
            if (!String.Equals(path, loadedPetPath, StringComparison.OrdinalIgnoreCase))
            {
                if (desktopPetSprite != null) { desktopPetSprite.Dispose(); desktopPetSprite = null; }
                loadedPetPath = path;
                try { if (settings.DesktopPetEnabled && File.Exists(path)) desktopPetSprite = new Bitmap(path); } catch { desktopPetSprite = null; }
            }
            if (!settings.DesktopPetEnabled || desktopPetSprite == null) return;

            float size = Math.Max(0.4f, Math.Min(3.0f, settings.DesktopPetSize / 100f));
            float aspect = desktopPetSprite.Height / Math.Max(1f, desktopPetSprite.Width);
            float baseW = 58f, baseH = baseW * aspect;
            if (baseH > 76f) { baseW *= 76f / baseH; baseH = 76f; }
            float petW = baseW * size;
            float left = 20f + petW * 0.5f;
            float right = Math.Max(left, LW - petW * 0.5f - 20f);
            petX = Math.Max(left, Math.Min(right, petX));

            // A small behaviour state machine: stroll, pause, glance around, then wander again.
            petBehaviourTimer -= dt;
            if (petBehaviourTimer <= 0f && petJump <= 0f && petJumpVelocity <= 0f)
            {
                if (petBehaviour == PetBehaviour.Walking)
                {
                    petBehaviour = PetBehaviour.Pausing;
                    petBehaviourTimer = PetRandom(0.65f, 1.7f);
                }
                else if (petBehaviour == PetBehaviour.Pausing)
                {
                    petBehaviour = PetBehaviour.Looking;
                    petBehaviourTimer = PetRandom(0.8f, 1.9f);
                }
                else
                {
                    petBehaviour = PetBehaviour.Walking;
                    petBehaviourTimer = PetRandom(4.0f, 9.5f);
                    if (petRandom.NextDouble() < 0.28) petDirection *= -1f;
                }
            }

            if (petBehaviour == PetBehaviour.Walking && petJump <= 0f && petJumpVelocity <= 0f)
            {
                petX += petDirection * 20f * dt;
                petWalkPhase += dt * 8.5f;
                petWalkBob = Math.Abs((float)Math.Sin(petWalkPhase)) * 2.1f;
                if (petX <= left) { petX = left; petDirection = 1f; }
                if (petX >= right) { petX = right; petDirection = -1f; }
                petLookAngle = Theme.Smooth(petLookAngle, 0f, 7f, dt);
            }
            else if (petBehaviour == PetBehaviour.Looking)
            {
                petWalkBob = Theme.Smooth(petWalkBob, 0f, 8f, dt);
                // Gentle left-right head/body tilt reads as scanning the surroundings.
                petLookAngle = (float)Math.Sin((time * 4.5f) + 0.35f) * 10.5f;
            }
            else
            {
                petWalkBob = Theme.Smooth(petWalkBob, 0f, 8f, dt);
                petLookAngle = Theme.Smooth(petLookAngle, 0f, 5f, dt);
            }

            petNextJump -= dt;
            if (petNextJump <= 0f && petJump <= 0f && petJumpVelocity <= 0f)
            {
                // Jumps are occasional, not rhythmic. Usually happen while strolling.
                if (petRandom.NextDouble() < 0.72 && petBehaviour != PetBehaviour.Looking)
                {
                    petJumpVelocity = PetRandom(245f, 300f);
                    petSpin = 0f;
                    petLandingSquash = 0f;
                }
                petNextJump = PetRandom(5.0f, 12.0f);
            }

            if (petJump > 0f || petJumpVelocity > 0f)
            {
                petJump += petJumpVelocity * dt;
                petJumpVelocity -= 520f * dt;
                if (petJump <= 0f)
                {
                    petJump = 0f; petJumpVelocity = 0f; petSpin = 0f;
                    petLandingSquash = 0.32f;
                    if (petRandom.NextDouble() < 0.2) { petBehaviour = PetBehaviour.Pausing; petBehaviourTimer = PetRandom(0.35f, 0.8f); }
                }
                else
                {
                    // One full rotation across a jump arc, with a little extra rotation variation.
                    petSpin += 380f * dt;
                }
            }
            petLandingSquash = Theme.Smooth(petLandingSquash, 0f, 9f, dt);
        }

        private bool Step(float dt)
        {
            bool busy = false;
            var set = store.Data.Settings;
            float k = set.Intensity / 100f * 1.6f;
            player.Volume = set.SoundVolume / 100f;
            coolMusic.Volume = set.MusicVolume / 100f;
            if (set.CoolMode != previousCoolMode)
            {
                previousCoolMode = set.CoolMode;
                discordPresence.Update(set.CoolMode);
                if (set.CoolMode)
                {
                    string musicPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "RainbowRoad.mp3");
                    if (File.Exists(musicPath)) { try { coolMusic.Play("cool-mode-music", musicPath, store.Data.Settings.OutputDevice); } catch (Exception ex) { Log.Warn("audio", "cool mode music could not start", ex); } }
                }
                else coolMusic.Stop();
                forceFrame = true;
            }
            string requestedBg = set.BackgroundImagePath ?? "";
            if (!String.Equals(requestedBg, loadedBackgroundPath, StringComparison.OrdinalIgnoreCase))
            {
                if (galaxyBackground != null) { galaxyBackground.Dispose(); galaxyBackground = null; }
                loadedBackgroundPath = requestedBg;
                try { if (File.Exists(requestedBg)) galaxyBackground = new Bitmap(requestedBg); } catch { galaxyBackground = null; }
                InvalidateBackdrop();
                forceFrame = true;
            }
            bool hoverAnim = set.HoverAnimations;

            // --- background and glow
            // Keep the signature space background alive and cursor-reactive even if an older
            // saved settings file had these effects switched off.
            bgLevel = Ease(bgLevel, 1f, 4f, dt, ref busy);
            reactLevel = Ease(reactLevel, 1f, 4f, dt, ref busy);
            glowLevel = Ease(glowLevel, (set.CursorGlow && hasMouse) ? 1f : 0f, 6f, dt, ref busy);

            if (hasMouse)
            {
                if (!glowInit) { glowX = mx; glowY = my; glowInit = true; }
                else
                {
                    glowX = Theme.Smooth(glowX, mx, 14f, dt);
                    glowY = Theme.Smooth(glowY, my, 14f, dt);
                    if (Math.Abs(glowX - mx) > 0.3f || Math.Abs(glowY - my) > 0.3f) busy = true;
                }
            }
            else glowInit = false;

            bool fieldLive = bgLevel > 0.004f || reactLevel > 0.004f;
            if (fieldLive) fieldSettle = 1.5f; else fieldSettle -= dt;
            if (fieldSettle > 0f)
            {
                hazeTime += dt * bgLevel * (0.4f + k);
                field.Update(dt, LW, LH, hasMouse, mx, my, bgLevel, reactLevel, k);
                busy = true;
            }

            // --- layout and scrolling
            LayoutCards();
            float prevScroll = scrollY;
            scrollY = Theme.Smooth(scrollY, scrollTarget, 14f, dt);
            if (Math.Abs(scrollY - scrollTarget) < 0.3f) scrollY = scrollTarget;
            if (Math.Abs(scrollY - prevScroll) > 0.001f) { busy = true; scrollFade = 1.4f; }
            if (scrollFade > 0f) { scrollFade -= dt; busy = true; }

            // --- cards
            int rebuilt = 0;
            for (int i = cards.Count - 1; i >= 0; i--)
            {
                Card c = cards[i];

                if (c.Removing)
                {
                    c.Appear -= dt / 0.25f;
                    busy = true;
                    if (c.Appear <= 0f)
                    {
                        if (hover.Card == c) hover = new Hit();
                        if (pressed.Card == c) { pressed = new Hit(); pressing = false; }
                        c.Dispose();
                        cards.RemoveAt(i);
                        viewReady = false;
                        continue;
                    }
                }
                else if (c.Appear < 1f)
                {
                    c.Appear = Math.Min(1f, c.Appear + dt / 0.38f);
                    busy = true;
                }

                if (!c.PosInit)
                {
                    c.X = c.TargetX; c.Y = c.TargetY; c.W = c.TargetW;
                    c.PosInit = true;
                }
                else if (!c.Removing)
                {
                    c.X = Ease(c.X, c.TargetX, 11f, dt, ref busy);
                    c.Y = Ease(c.Y, c.TargetY, 11f, dt, ref busy);
                    c.W = Ease(c.W, c.TargetW, 14f, dt, ref busy);
                }

                bool isHover = hover.Kind == HitKind.Card && hover.Card == c;
                float hoverT = isHover ? 1f : 0f;
                if (hoverAnim) c.Hover = Ease(c.Hover, hoverT, 13f, dt, ref busy);
                else c.Hover = hoverT;

                bool pressOn = pressing && pressed.Kind == HitKind.Card && pressed.Card == c &&
                               (pressed.Part == CardPart.Body || pressed.Part == CardPart.Play);
                c.Press = Ease(c.Press, pressOn ? 1f : 0f, 24f, dt, ref busy);
                c.PlayHover = Ease(c.PlayHover, isHover && hover.Part == CardPart.Play ? 1f : 0f, 16f, dt, ref busy);
                c.DeleteHover = Ease(c.DeleteHover, isHover && hover.Part == CardPart.Delete ? 1f : 0f, 16f, dt, ref busy);
                c.EditHover = Ease(c.EditHover, isHover && hover.Part == CardPart.Edit ? 1f : 0f, 16f, dt, ref busy);

                bool isActive = player.IsPlaying && player.CurrentId == c.Item.Id;
                c.Active = Ease(c.Active, isActive ? 1f : 0f, 10f, dt, ref busy);
                if (c.Active > 0.01f) busy = true;

                if (c.RippleT < 1f)
                {
                    c.RippleT = Math.Min(1f, c.RippleT + dt * 1.7f);
                    busy = true;
                }

                // Artwork: only keep it for cards near the screen, rebuild it once the size has settled.
                float sy = GridTop + c.Y - scrollY;
                bool near = sy + CardH > GridTop - 250f && sy < GridBottom + 250f;
                if (!near)
                {
                    if (sy + CardH < GridTop - 900f || sy > GridBottom + 900f) c.DropCover();
                }
                else if (rebuilt < 4 && Math.Abs(c.W - c.TargetW) < 0.5f)
                {
                    int pw = (int)Math.Round(c.TargetW * scale);
                    int ph = (int)Math.Round(CardH * scale);
                    if (c.Cover == null || c.CoverW != pw || c.CoverH != ph)
                    {
                        c.BuildCover(store.ImagePath(c.Item), pw, ph, scale);
                        rebuilt++;
                        busy = true;
                    }
                }
            }

            // --- header buttons
            addHover = Ease(addHover, hover.Kind == HitKind.Add ? 1f : 0f, 14f, dt, ref busy);
            addPress = Ease(addPress, (pressing && pressed.Kind == HitKind.Add) ? 1f : 0f, 24f, dt, ref busy);
            setHover = Ease(setHover, hover.Kind == HitKind.Settings ? 1f : 0f, 14f, dt, ref busy);
            emptyHover = Ease(emptyHover, hover.Kind == HitKind.Empty ? 1f : 0f, 12f, dt, ref busy);
            if (setHover > 0.01f)
            {
                gearAngle = (gearAngle + dt * 90f * setHover) % 360f;
                busy = true;
            }

            // --- playback display
            if (player.IsPlaying)
            {
                state = PlayState.Playing;
                lastPos = player.Position;
                lastDur = player.Duration;
                float frac = lastDur.TotalSeconds > 0.001 ? (float)(lastPos.TotalSeconds / lastDur.TotalSeconds) : 0f;
                frac = Theme.Clamp01(frac);
                if (frac < progressShown - 0.3f) progressShown = frac;
                else progressShown = Theme.Smooth(progressShown, frac, 30f, dt);
                busy = true;
            }
            else if (state == PlayState.Complete)
            {
                progressShown = Theme.Smooth(progressShown, 1f, 12f, dt);
                stateTimer -= dt;
                busy = true;
                if (stateTimer <= 0f) { state = PlayState.Ready; lastPos = TimeSpan.Zero; lastDur = TimeSpan.Zero; }
            }
            else if (state == PlayState.Error)
            {
                stateTimer -= dt;
                progressShown = Ease(progressShown, 0f, 7f, dt, ref busy);
                busy = true;
                if (stateTimer <= 0f) state = PlayState.Ready;
            }
            else
            {
                progressShown = Ease(progressShown, 0f, 7f, dt, ref busy);
            }

            // --- toast
            if (toastLife > 0f) { toastLife -= dt; busy = true; }

            return busy;
        }
    }
}
