using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace OVE.Service.NetworkTiles.QuadTree.Utilities {
    public class UnicodeRemoval {
        public static void ReWriteFileWithoutUnicode(string filename) {

            if (filename == null || !File.Exists(filename)) throw new ArgumentNullException(filename);
            var tempFilename = Path.ChangeExtension(filename, ".backup");

            Regex r = new Regex(
                @"[^\u0000-\u007F]+"); //https://stackoverflow.com/questions/123336/how-can-you-strip-non-ascii-characters-from-a-string-in-c
            File.WriteAllLines(tempFilename, File.ReadLines(filename).Select(l => r.Replace(l, string.Empty)));

            File.Copy(tempFilename, filename, true);
            File.Delete(tempFilename);
        }
    }
}
