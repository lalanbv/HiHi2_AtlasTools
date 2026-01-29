using System;
using UnityEngine;

namespace HiHi2.AtlasTools
{
    public class TextureInfo : IDisposable
    {
        public string name;
        public Texture2D texture;
        public string texturePath;
        public int width;
        public int height;
        public int area;
        public int originalWidth;
        public int originalHeight;
        public bool isDownscaled;
        public Texture2D readableTexture;

        private bool _disposed;

        public int PaddedWidth(int padding) => width + padding * 2;
        public int PaddedHeight(int padding) => height + padding * 2;
        public int PaddedArea(int padding) => PaddedWidth(padding) * PaddedHeight(padding);

        public bool IsValid => texture != null && width > 0 && height > 0;

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing && readableTexture != null)
            {
                UnityEngine.Object.DestroyImmediate(readableTexture);
                readableTexture = null;
            }

            _disposed = true;
        }

        ~TextureInfo()
        {
            Dispose(false);
        }
    }
}