namespace MadsKristensen.ImageOptimizer
{
    using System;
    
    /// <summary>
    /// Helper class that exposes all GUIDs used across VS Package.
    /// </summary>
    internal sealed partial class GuidList
    {
        public const string guidImageOptimizerPkgString = "bf95754f-93d3-42ff-bfe3-e05d23188b08";
        public const string guidImageOptimizerCmdSetString = "d3662d85-2693-4ff5-97aa-3878453c787b";
        public const string guidImagesString = "e0d8d968-5115-44d4-be14-c7c68d469d27";
        public static Guid guidImageOptimizerPkg = new Guid(guidImageOptimizerPkgString);
        public static Guid guidImageOptimizerCmdSet = new Guid(guidImageOptimizerCmdSetString);
        public static Guid guidImages = new Guid(guidImagesString);
    }
    /// <summary>
    /// Helper class that encapsulates all CommandIDs uses across VS Package.
    /// </summary>
    internal sealed partial class PackageCommands
    {
        public const int MyMenuGroup = 0x1020;
        public const int cmdOptimizeImage = 0x0100;
        public const int cmdCopyDataUri = 0x0200;
        public const int optimize = 0x0001;
        public const int copy = 0x0002;
    }
}
