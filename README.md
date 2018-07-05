# Image Optimizer for Visual Studio

[![Build status](https://ci.appveyor.com/api/projects/status/26hxodkud0i54rv5?svg=true)](https://ci.appveyor.com/project/madskristensen/imageoptimizer)

Download this extension from the [VS Gallery](https://visualstudiogallery.msdn.microsoft.com/a56eddd3-d79b-48ac-8c8f-2db06ade77c3)
or get the [nightly build](http://vsixgallery.com/extension/bf95754f-93d3-42ff-bfe3-e05d23188b08/).

--------------------------------

Uses industry standard tools to optimize any JPEG, PNG
and Gifs - including animated Gifs. It can do both lossy
and lossless optimization.

## Features

Adds a right-click menu to any folder and image in Solution Explorer
that let's you automatically optimize all PNG, GIF and JPEG files
in that folder. 

- Optimizes PNGs (uses Zopfli compression)
- Optimizes GIFs
- Optimizes animated GIFs
- Optimizes JPGs (uses MozJPEG)
- Works on single images files or entire folders
- Copy any image as base64 dataURI to clipboard

## Optimize images
Simply right-click any file or folder containing images and click 
one of the image optimization buttons.

![Context menu](art/context-menu.png)

You can also right-click a folder to optimize all images inside it.

### Best quality 
If you chose best quality optimization, the tool will
do its optimizations without changing the quality of the image.

### Best compression
If you decide to sacrifice just a small amount of image quality
(which in most cases is unnoticeable to the human eye), you will
be able to save up to 90% of the initial file weight. Lossy
optimization will give you outstanding results with just a
fraction of image quality loss.

## Output window
The Output Window shows the detailed output from the optimization
process and progress is displayed in the status bar.

![Output window](art/output-window.png)

## Performance
Optimizing an image can easily take several seconds which feels
slow. This extension parallelizes the workload on each CPU core
available on the machine. This speeds up the optimization
significantly.

## API
Any extension can call the commands provided in the Image Optimizer extension to optimize any image. 

```c#
public void OptimizeImage(string filePath)
{
	try
	{
		var DTE = (DTE2)Package.GetGlobalService(typeof(DTE));
		Command command = DTE.Commands.Item("ImageOptimizer.OptimizeLossless");

		if (command != null && command.IsAvailable)
		{
			DTE.Commands.Raise(command.Guid, command.ID, filePath, null);
		}
	}
	catch (Exception ex)
	{
		// Image Optimizer not installed
	}
}
```

The commands are:

* ImageOptimizer.OptimizeLossless - *Optimize for best quality*
* ImageOptimizer.OptimizeLossy - *Optimize for best compression*

## Contribute
Check out the [contribution guidelines](.github/CONTRIBUTING.md)
if you want to contribute to this project.

For cloning and building this project yourself, make sure 
to install the
[Extensibility Tools 2015](https://visualstudiogallery.msdn.microsoft.com/ab39a092-1343-46e2-b0f1-6a3f91155aa6)
extension for Visual Studio which enables some features
used by this project.

## License
[Apache 2.0](LICENSE) 