using Microsoft.AspNetCore.Mvc;

namespace TagEditor.Controllers;
public class ListenController : Controller
{
    [HttpGet("listen/get")]
    public IActionResult Get(string path)
    {
        if (path.EndsWith(".mp3"))
        {
            Response.Headers.AcceptRanges = "bytes";
            return File(System.IO.File.OpenRead(path), "audio/mpeg");
        }
        else throw null!;
    }
}
