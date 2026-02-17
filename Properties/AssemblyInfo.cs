using System.Reflection;
using System.Runtime.InteropServices;

// [MANDATORY] The following GUID is used as a unique identifier of the plugin. Generate a fresh one for your plugin!
[assembly: Guid("9075c999-dacb-4c24-9e14-b696a1ac9e89")]

// [MANDATORY] The assembly versioning
//Should be incremented for each new release build of a plugin

// Odd minor releases for Beta
[assembly: AssemblyVersion("4.0.0.60")]
[assembly: AssemblyFileVersion("4.0.0.60")]

// [MANDATORY] The name of your plugingit st
[assembly: AssemblyTitle("Sequencer Powerups")]
// [MANDATORY] A short description of your plugin
[assembly: AssemblyDescription("*** BETA RELEASE ***")]
//[assembly: AssemblyDescription("Get the most out of the Advanced Sequencer!")]

// The following attributes are not required for the plugin per se, but are required by the official manifest meta data

// Your name
[assembly: AssemblyCompany("Marc Blank")]
// The product name that this plugin is part ofgit 
[assembly: AssemblyProduct("When")] 
[assembly: AssemblyCopyright("Copyright © 2023-2026 Marc Blank")]

// The minimum Version of N.I.N.A. that this plugin is compatible withq
[assembly: AssemblyMetadata("MinimumApplicationVersion", "3.3.0.1000")]

// The license your plugin code is using
[assembly: AssemblyMetadata("License", "MPL-2.0")]
// The url to the license
[assembly: AssemblyMetadata("LicenseURL", "https://www.mozilla.org/en-US/MPL/2.0/")]
// The repository where your pluggin is hosted
[assembly: AssemblyMetadata("Repository", "https://bitbucket.org/zorkmid/nina.plugin.when")]

// The following attributes are optional for the official manifest meta data

//[Optional] Common tags that quickly describe your plugin
[assembly: AssemblyMetadata("Tags", "Sequencer,Utility,Powerups,Conditionals,Safety,Interrupt,If,When")]

//[Optional] The url to a featured logo that will be displayed in the plugin list next to the name
[assembly: AssemblyMetadata("FeaturedImageURL", "https://github.com/marcblank/powerups4.github.io/blob/main/Powerups.png")]
[assembly: AssemblyMetadata("ScreenshotURL", "https://github.com/marcblank/powerups4.github.io/blob/main/WBU.png")]
//[Optional] An additional url to an example example screenshot of your plugin in action
[assembly: AssemblyMetadata("AltScreenshotURL", "https://1drv.ms/u/s!AjBSqKNCEWOTgfIGHf3eIXv2hZfYAw?e=LLHMJF")]
//[Optional] An in-depth description of your plugin
[assembly: AssemblyMetadata("LongDescription", @"## This plugin contains a variety of instructions that enhance the power of the Advanced Sequencer.

## Interim documentation for Sequencer Powerups 4 is at [Powerups Docs](https://marcblank.github.io/powerups4.github.io/)

# Powerups 4 (For NINA 3.3) Notes/Warnings

Here are things you need to consider before using Powerups 4:

- All + instructions (except Smart Subframe Exposure +) will automatically be converted to NINA instructions
- Smart Subframe Exposure + has been removed; please use NINA's Take Subframe Exposure instruction
- Constants and Variables are now referred to generically as Symbols
- Constant/Variable containers are now plain Sequential Instruction Sets; Symbols defined in them become global in scope
- Local Variables are now called Scoped Variables
- Local Constants no longer exist; they are automatically converted to Scoped (Local) Variables
- Constants defined in the Powerups plugin page are no longer available.
- The Call and Return instructions aren't initially available
- The SAFE Variable is no longer supported; Wait Until Safe + becomes core Wait Until Safe
- The use of the NINAESRC environment variable is no longer supported in External Script
- TS_ (Target Scheduler) Variables aren't available until implemented in Target Scheduler
- Names of some Variables have been changed, but the upgrade process should rename them; you'll still want to check your sequences/templates for newly undefined Variables in case something was missed

## Comments, suggestions, bug reports, etc. are welcomed!  Contact me by DM @Marc on the NINA Discord server, or post in the #sequencer-powerups channel.
")]

// Setting ComVisible to false makes the types in this assembly not visible
// to COM components.  If you need to access a type in this assembly from
// COM, set the ComVisible attribute to true on that type.
[assembly: ComVisible(false)]
// [Unused]
[assembly: AssemblyConfiguration("")]
// [Unused]
[assembly: AssemblyTrademark("")]
// [Unused]
[assembly: AssemblyCulture("")]