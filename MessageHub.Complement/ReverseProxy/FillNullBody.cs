using System.Text;

namespace MessageHub.Complement.ReverseProxy;

public class FillNullBody : IMiddleware
{
    private class FillNullBodyStream : Stream
    {
        private readonly Stream stream;
        private readonly Stream nullStream;
        private bool readStreamEnded;
        private bool hasData;

        public FillNullBodyStream(Stream stream)
        {
            this.stream = stream;
            readStreamEnded = false;
            hasData = false;
            nullStream = new MemoryStream(Encoding.UTF8.GetBytes("null"));
        }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length
        {
            get
            {
                long length = stream.Length;
                if (length == 0)
                {
                    length = nullStream.Length;
                }
                return length;
            }
        }

        public override long Position
        {
            get => stream.Position + nullStream.Position;
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
            throw new NotSupportedException();
        }

        public override async ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            if (!readStreamEnded)
            {
                int readBytes = await stream.ReadAsync(buffer, cancellationToken);
                if (readBytes > 0)
                {
                    hasData = true;
                }
                else
                {
                    readStreamEnded = true;
                    if (!hasData)
                    {
                        readBytes = nullStream.Read(buffer.Span);
                    }
                }
                return readBytes;
            }
            else
            {
                int readBytes = 0;
                if (!hasData)
                {
                    readBytes = nullStream.Read(buffer.Span);
                }
                return readBytes;
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (!readStreamEnded)
            {
                int readBytes = stream.Read(buffer, offset, count);
                if (readBytes > 0)
                {
                    hasData = true;
                }
                else
                {
                    readStreamEnded = true;
                    if (!hasData)
                    {
                        readBytes = nullStream.Read(buffer, offset, count);
                    }
                }
                return readBytes;
            }
            else
            {
                int readBytes = 0;
                if (!hasData)
                {
                    readBytes = nullStream.Read(buffer, offset, count);
                }
                return readBytes;
            }
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }
    }

    public Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        if (context.Request.ContentLength is null || context.Request.ContentLength == 0)
        {
            context.Request.ContentLength = null;
            context.Request.Body = new FillNullBodyStream(context.Request.Body);
        }
        return next(context);
    }
}

public class FillNullBodyPipeline
{
    public void Configure(IApplicationBuilder app) => app.UseMiddleware<FillNullBody>();
}
