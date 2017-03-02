YoutubeExplode
===================


Zero-dependency .NET library that parses public meta data on Youtube videos


**Download:**

The library is distributed as a [nuget package](https://www.nuget.org/packages/YoutubeExplode): `Install-Package YoutubeExplode`

You can also find the last stable version in [releases](https://github.com/Tyrrrz/YoutubeExplode/releases)

**Features:**

- Gets video meta and video streams meta
- Supports stream meta data from multiple sources: non-adaptive, adaptive embedded, adaptive dash
- Meta data properties are exposed via enums and other strong types
- Deciphers signatures for video streams automatically or on-demand
- Gets file sizes for video streams automatically or on-demand
- Downloads video to stream
- Exposes static methods to extract video ID from URL and to validate video ID
- Asynchronous API
- Caches consistent resources to increase performance
- Allows substitution of the default HTTP handler to implement custom logic
- XML documentation for every public member
- Built against .NET Standard 1.1 and supports most concrete .NET Frameworks (refer to [this](https://github.com/dotnet/standard/blob/master/docs/versions.md))

**Parsed meta data:**

- Video id, title and author
- Length
- View count
- Rating
- Search keywords
- URLs of thumbnail, high/medium/low quality images
- URLS of watermarks
- Is listed, is muted, allows ratings, allows embedding, has closed captions
- Video streams
- Caption tracks

Video stream objects include the following meta data:

- URL
- Type
- Quality
- Resolution
- Bitrate
- FPS
- File size

Caption track objects include the following meta data:

- URL
- Language

**Usage:**

Check out `YoutubeExplodeDemoConsole` or `YoutubeExplodeDemoWpf` projects for real examples.

```c#
using System;
using System.Linq;
using YoutubeExplode;

// Create client instance
var client = new YoutubeClient();

// Get video info
var videoInfo = await client.GetVideoInfoAsync("bx_KorIwABQ");

// Output some of it to console
Console.WriteLine($"Title: {videoInfo.Title}");
Console.WriteLine($"Author: {videoInfo.Author}");
Console.WriteLine($"Length: {videoInfo.Length}");

// Download the highest quality video stream to file
var streamInfo = videoInfo.Streams.OrderBy(s => (int) s.Quality).First();
string fileName = $"{videoInfo.Id}_{streamInfo.Quality}.{streamInfo.FileExtension}";
using (var input = await client.DownloadVideo(streamInfo))
using (var output = new File.Create(fileName))
    await input.CopyToAsync(output);

```

**Libraries used:**

Console demo:

- [Tyrrrz.Extensions](https://github.com/Tyrrrz/Extensions) - my set of various extensions for rapid development

Wpf demo:

- [GalaSoft.MVVMLight](http://www.mvvmlight.net) - MVVM rapid development
- [MaterialDesignXAML](https://github.com/ButchersBoy/MaterialDesignInXamlToolkit) - MaterialDesign UI
- [Tyrrrz.Extensions](https://github.com/Tyrrrz/Extensions) - my set of various extensions for rapid development
- [Tyrrrz.WpfExtensions](https://github.com/Tyrrrz/WpfExtensions) - my set of various WPF extensions for rapid development
 
**Screenshots:**

![](http://www.tyrrrz.me/projects/images/ytexplode_1.png)
![](http://www.tyrrrz.me/projects/images/ytexplode_2.png)
