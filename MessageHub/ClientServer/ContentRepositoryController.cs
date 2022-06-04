using System.Text.RegularExpressions;
using MessageHub.Authentication;
using MessageHub.HomeServer;
using MessageHub.HomeServer.Remote;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MessageHub.ClientServer;

[Route("_matrix/media/{version}")]
public class ContentRepositoryController : ControllerBase
{
    private readonly IIdentityService identityService;
    private readonly IContentRepository contentRepository;
    private readonly IRemoteContentRepository remoteContentRepository;

    public ContentRepositoryController(
        IIdentityService identityService,
        IContentRepository contentRepository,
        IRemoteContentRepository remoteContentRepository)
    {
        ArgumentNullException.ThrowIfNull(identityService);
        ArgumentNullException.ThrowIfNull(contentRepository);
        ArgumentNullException.ThrowIfNull(remoteContentRepository);

        this.identityService = identityService;
        this.contentRepository = contentRepository;
        this.remoteContentRepository = remoteContentRepository;
    }

    [Route("config")]
    [HttpGet]
    [Authorize(AuthenticationSchemes = MatrixAuthenticationSchemes.Client)]
    public IActionResult GetConfig()
    {
        return new JsonResult(new Dictionary<string, object>
        {
            ["m.upload.size"] = int.MaxValue
        });
    }

    private static bool IsValidString(string s)
    {
        return Regex.IsMatch(s, "^[A-Za-z0-9_-]+$");
    }

    [Route("download/{serverName}/{mediaId}")]
    [HttpGet]
    public async Task<IActionResult> Download(
        [FromRoute] string serverName,
        [FromRoute] string mediaId,
        [FromQuery(Name = "allow_remote")] bool? allowRemote)
    {
        if (!IsValidString(serverName))
        {
            return BadRequest(MatrixError.Create(
                MatrixErrorCode.InvalidParameter,
                $"{nameof(serverName)}: {serverName}"));
        }
        if (!IsValidString(mediaId))
        {
            return BadRequest(MatrixError.Create(
                MatrixErrorCode.InvalidParameter,
                $"{nameof(mediaId)}: {mediaId}"));
        }
        if (!identityService.HasSelfIdentity || serverName == identityService.GetSelfIdentity().Id)
        {
            var stream = await contentRepository.DownloadFileAsync(serverName, mediaId);
            if (stream is not null)
            {
                return File(stream, "application/octet-stream", mediaId);
            }
        }
        else if (allowRemote != false)
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(1));
            var stream = await remoteContentRepository.DownloadFileAsync(serverName, mediaId, cts.Token);
            if (stream is not null)
            {
                return File(stream, "application/octet-stream", mediaId);
            }
        }
        return NotFound(MatrixError.Create(MatrixErrorCode.NotFound));
    }

    [Route("download/{serverName}/{mediaId}/{fileName}")]
    [HttpGet]
    public async Task<IActionResult> Download(
        [FromRoute] string serverName,
        [FromRoute] string mediaId,
        [FromRoute] string fileName,
        [FromQuery(Name = "allow_remote")] bool? allowRemote)
    {
        if (!IsValidString(serverName))
        {
            return BadRequest(MatrixError.Create(
                MatrixErrorCode.InvalidParameter,
                $"{nameof(serverName)}: {serverName}"));
        }
        if (!IsValidString(mediaId))
        {
            return BadRequest(MatrixError.Create(
                MatrixErrorCode.InvalidParameter,
                $"{nameof(mediaId)}: {mediaId}"));
        }
        if (!identityService.HasSelfIdentity || serverName == identityService.GetSelfIdentity().Id)
        {
            var stream = await contentRepository.DownloadFileAsync(serverName, mediaId);
            if (stream is not null)
            {
                return File(stream, "application/octet-stream", fileName);
            }
        }
        else if (allowRemote != false)
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(1));
            var stream = await remoteContentRepository.DownloadFileAsync(serverName, mediaId, cts.Token);
            if (stream is not null)
            {
                return File(stream, "application/octet-stream", fileName);
            }
        }
        return NotFound(MatrixError.Create(MatrixErrorCode.NotFound));
    }

    [Route("thumbnail/{serverName}/{mediaId}")]
    [HttpGet]
    public Task<IActionResult> DownloadThumbnail(
        [FromRoute] string serverName,
        [FromRoute] string mediaId,
        [FromQuery(Name = "allow_remote")] bool? allowRemote)
    {
        return Download(serverName, mediaId, allowRemote);
    }

    [Route("upload")]
    [HttpPost]
    [Authorize(AuthenticationSchemes = MatrixAuthenticationSchemes.Client)]
    public async Task<IActionResult> Upload()
    {
        string url = await contentRepository.UploadFileAsync(Request.Body);
        return new JsonResult(new { content_uri = url });
    }
}
