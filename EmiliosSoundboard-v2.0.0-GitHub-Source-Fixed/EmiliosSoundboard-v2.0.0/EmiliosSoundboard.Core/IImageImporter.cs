namespace EmiliosSoundboard
{
    /// <summary>
    /// Turns an arbitrary user image into a file inside the app's Backgrounds folder.
    /// Implemented by the Windows front end (System.Drawing is Windows-only in .NET 8);
    /// the library itself only needs the resulting file name.
    /// </summary>
    public interface IImageImporter
    {
        /// <summary>
        /// Copies/normalises <paramref name="sourcePath"/> into <paramref name="imagesDir"/>
        /// and returns the bare file name that was created there.
        /// </summary>
        string Import(string sourcePath, string imagesDir);
    }
}
