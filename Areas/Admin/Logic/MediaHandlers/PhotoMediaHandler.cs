﻿using System.Drawing;
using Bonsai.Data.Models;

namespace Bonsai.Areas.Admin.Logic.MediaHandlers
{
    /// <summary>
    /// MediaHandler for creating thumbnails for photos.
    /// </summary>
    public class PhotoMediaHandler: IMediaHandler
    {
        private static string[] _supportedMimeTypes =
        {
            "image/jpeg",
            "image/png"
        };

        /// <summary>
        /// Flag indicating that the media does not need any encoding.
        /// </summary>
        public bool IsImmediate => true;

        /// <summary>
        /// Supported file types.
        /// </summary>
        public string[] SupportedMimeTypes => _supportedMimeTypes;

        /// <summary>
        /// Resulting media type.
        /// </summary>
        public MediaType MediaType => MediaType.Photo;

        /// <summary>
        /// Returns the image for thumnail creation.
        /// </summary>
        public Image ExtractThumbnail(string path, string mime)
        {
            return Image.FromFile(path);
        }
    }
}
