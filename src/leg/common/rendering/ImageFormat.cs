namespace UK.Gov.Legislation.Common.Rendering {

internal static class ImageFormat {

    // Detects the raster format of rendered drawing bytes. Covers what LibreOffice HTML
    // export emits (GIF by default; PNG/JPEG if the docx passed a raster straight through).
    // Unknown bytes are labelled gif/image/gif since LegImageProcessor converts to GIF anyway.
    internal static (string Ext, string Mime) Detect(byte[] b) {
        if (b == null || b.Length < 4) return ("gif", "image/gif");
        if (b[0] == 0x89 && b[1] == (byte)'P' && b[2] == (byte)'N' && b[3] == (byte)'G')
            return ("png", "image/png");
        if (b[0] == (byte)'G' && b[1] == (byte)'I' && b[2] == (byte)'F')
            return ("gif", "image/gif");
        if (b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF)
            return ("jpg", "image/jpeg");
        return ("gif", "image/gif");
    }

}

}
