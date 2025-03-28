[marketplace]: https://marketplace.visualstudio.com/items?itemName=MadsKristensen.ImageOptimizer64bit
[vsixgallery]: http://vsixgallery.com/extension/fc4e241f-57de-4032-9c89-527984c0a0ae/
[repo]:https://github.com/madskristensen/ImageOptimizer

# Image Optimizer for Visual Studio

[![Build](https://github.com/madskristensen/ImageOptimizer/actions/workflows/build.yaml/badge.svg)](https://github.com/madskristensen/ImageOptimizer/actions/workflows/build.yaml)
![GitHub Sponsors](https://img.shields.io/github/sponsors/madskristensen)

Download this extension from the [Visual Studio Marketplace][marketplace]
or get the [CI build][vsixgallery]

--------------------------------

Uses industry standard tools to optimize any JPEG, PNG, WebP, SVG,
and Gifs - including animated Gifs. It can do both lossy
and lossless optimization.

## Features

Adds a right-click menu to any folder and image in Solution Explorer
that let's you optimize all images in that folder. 

- **Optimize** PNG, JPG, WebP, SVG, and GIF (including animated GIFS) images
- Works on single images files or entire folders
- **Resize** images easily
- Copy any image as **base64 dataURI** to clipboard

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

## Image resize
You can resize images by using the *Resize Image* dialog. You get to it by right-clicking any single image (jpg, gif, and png only).

![Resize](art/resize.png)

## Output window
The Output Window shows the detailed output from the optimization
process and progress is displayed in the status bar.

![Output window](art/output-window.png)

## Performance
Optimizing an image can easily take several seconds which feels
slow. This extension parallelizes the workload on each CPU core
available on the machine. This speeds up the optimization
significantly.

## API for extenders
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

## How can I help?
If you enjoy using the extension, please give it a ★★★★★ rating on the [Visual Studio Marketplace][marketplace].

Should you encounter bugs or if you have feature requests, head on over to the [GitHub repo][repo] to open an issue if one doesn't already exist.

Pull requests are also very welcome, since I can't always get around to fixing all bugs myself. This is a personal passion project, so my time is limited.

Another way to help out is to [sponsor me on GitHub](https://github.com/sponsors/madskristensen).