﻿using System;
using System.IO;
using System.Threading.Tasks;
using Smartstore.Core.Content.Media.Imaging;
using Smartstore.IO;

namespace Smartstore.Core.Content.Media.Storage
{
    public abstract class MediaStorageItem : Disposable
    {
        private Stream _sourceStream;

        public Stream SourceStream
        {
            get
            {
                if (_sourceStream == null)
                    _sourceStream = GetSourceStreamAsync().Await();

                if (_sourceStream.CanSeek)
                    _sourceStream.Position = 0;

                return _sourceStream;
            }
        }

        protected abstract Task<Stream> GetSourceStreamAsync();

        public abstract Task SaveToAsync(Stream stream, MediaFile mediaFile);

        protected override void OnDispose(bool disposing)
        {
            if (disposing && _sourceStream != null)
            {
                _sourceStream.Dispose();
                _sourceStream = null;
            }
        }

        #region Factories

        public static MediaStorageItem FromImage(IImage image)
        {
            return new ImageStorageItem(image);
        }

        public static MediaStorageItem FromStream(Stream stream)
        {
            return new StreamStorageItem(stream);
        }

        public static MediaStorageItem FromFile(IFile file)
        {
            return new StreamStorageItem(file.OpenRead());
        }

        #endregion

        #region Impls

        public class ImageStorageItem : MediaStorageItem
        {
            private readonly IImage _image;

            public ImageStorageItem(IImage image)
            {
                _image = image;
            }

            protected override async Task<Stream> GetSourceStreamAsync()
            {
                var memStream = new MemoryStream();
                await _image.SaveAsync(memStream);
                return memStream;
            }

            public override async Task SaveToAsync(Stream stream, MediaFile mediaFile)
            {
                await _image.SaveAsync(stream);
                mediaFile.Size = (int)stream.Length;
            }
        }

        public class StreamStorageItem : MediaStorageItem
        {
            private readonly Stream _stream;

            public StreamStorageItem(Stream stream)
            {
                _stream = stream;
            }

            protected override Task<Stream> GetSourceStreamAsync()
            {
                return Task.FromResult(_stream);
            }

            public override async Task SaveToAsync(Stream stream, MediaFile mediaFile)
            {
                if (stream.CanSeek)
                {
                    stream.SetLength(0);
                }

                await SourceStream.CopyToAsync(stream);

                if (stream.CanSeek)
                {
                    stream.Position = 0;
                }

                mediaFile.Size = (int)stream.Length;
            }
        }

        #endregion
    }
}