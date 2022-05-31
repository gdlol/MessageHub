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

    [Route("download/{serverName}/{mediaId}")]
    [HttpGet]
    public async Task<IActionResult> Download(string serverName, string mediaId)
    {
        if (!identityService.HasSelfIdentity || serverName == identityService.GetSelfIdentity().Id)
        {
            string url = $"mxc://{serverName}/{mediaId}";
            var stream = await contentRepository.DownloadFileAsync(url);
            if (stream is not null)
            {
                return File(stream, "application/octet-stream", mediaId);
            }
        }
        else
        {
            var stream = await remoteContentRepository.DownloadFileAsync(serverName, mediaId);
            if (stream is not null)
            {
                return File(stream, "application/octet-stream", mediaId);
            }
        }
        return NotFound(MatrixError.Create(MatrixErrorCode.NotFound));
    }

    [Route("download/{serverName}/{mediaId}/{fileName}")]
    [HttpGet]
    public async Task<IActionResult> Download(string serverName, string mediaId, string fileName)
    {
        if (!identityService.HasSelfIdentity || serverName == identityService.GetSelfIdentity().Id)
        {
            string url = $"mxc://{serverName}/{mediaId}";
            var stream = await contentRepository.DownloadFileAsync(url);
            if (stream is not null)
            {
                return File(stream, "application/octet-stream", fileName);
            }
        }
        else
        {
            var stream = await remoteContentRepository.DownloadFileAsync(serverName, mediaId);
            if (stream is not null)
            {
                return File(stream, "application/octet-stream", fileName);
            }
        }
        return NotFound(MatrixError.Create(MatrixErrorCode.NotFound));
    }

    [Route("thumbnail/{serverName}/{mediaId}")]
    [HttpGet]
    public Task<IActionResult> DownloadThumbnail(string serverName, string mediaId)
    {
        return Download(serverName, mediaId);
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
