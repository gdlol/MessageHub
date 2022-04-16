using MessageHub.ClientServer.Protocol;
using MessageHub.HomeServer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MessageHub.ClientServer;

[Route("_matrix/media/{version}")]
public class ContentRepositoryController : ControllerBase
{
    private readonly IContentRepository contentRepository;

    public ContentRepositoryController(IContentRepository contentRepository)
    {
        ArgumentNullException.ThrowIfNull(contentRepository);

        this.contentRepository = contentRepository;
    }

    [Route("config")]
    [HttpGet]
    public IActionResult GetConfig()
    {
        return new JsonResult(new Dictionary<string, object>
        {
            ["m.upload.size"] = int.MaxValue
        });
    }

    [Route("download/{serverName}/{mediaId}")]
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Download(string serverName, string mediaId)
    {
        string url = $"mxc://{serverName}/{mediaId}";
        var stream = await contentRepository.DownloadFileAsync(url);
        if (stream is null)
        {
            return NotFound(MatrixError.Create(MatrixErrorCode.NotFound));
        }
        else
        {
            return File(stream, "application/octet-stream", mediaId);
        }
    }

    [Route("download/{serverName}/{mediaId}/{fileName}")]
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Download(string serverName, string mediaId, string fileName)
    {
        string url = $"mxc://{serverName}/{mediaId}";
        var stream = await contentRepository.DownloadFileAsync(url);
        if (stream is null)
        {
            return NotFound(MatrixError.Create(MatrixErrorCode.NotFound));
        }
        else
        {
            return File(stream, "application/octet-stream", fileName);
        }
    }

    [Route("thumbnail/{serverName}/{mediaId}")]
    [HttpGet]
    [AllowAnonymous]
    public Task<IActionResult> DownloadThumbnail(string serverName, string mediaId)
    {
        return Download(serverName, mediaId);
    }

    [Route("upload")]
    [HttpPost]
    public async Task<IActionResult> Upload()
    {
        string url = await contentRepository.UploadFileAsync(Request.Body);
        return new JsonResult(new { content_uri = url });
    }
}
