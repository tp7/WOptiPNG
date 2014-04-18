## WOptiPNG - simple GUI for OptiPNG. ##

This is a very basic [GUI](http://i.imgur.com/RsD3W9N.png) for [OptiPNG](http://optipng.sourceforge.net/) optimizer. 

#### Why optimize PNG at all? ####
If you take and share a lot of screenshots from MPC, or just use not very efficient apps like MS Paint, you can decrease the filesize by about 30-40% just by using OptiPNG with default mode 2. It's a fast and easy way to save some traffic.

#### Why GUI? ####
Two reasons. 
First, you can drag and drop folders onto the app and it'll optionally include all PNG files inside all subfolders.
Second, the GUI will launch multiple OptiPNG processes in parallel to maximize your CPU usage. OptiPNG itself is singlethreaded. 

#### Key differences to [PNGGauntlet](http://pnggauntlet.com/) ####

 1. Awful modern design (subjective).
 2. Only OptiPNG is supported. It's enough for absolutely most cases.
 3. Only PNG input is supported. The rest is easy to allow if requested.
 4. Cancelling optimization doesn't kill OptiPNG processes. Can be implemented if requested.
 3. You can set exact number of parallel processes to use instead of just toggling multithreading.
 4. UTF-16 paths are supported (unless you have utf-16 characters in your %TEMP% path).
 5. Double-click or Enter key opens selected image.
 6. You can see [OptiPNG log](http://i.imgur.com/A3QNZHL.png) on mouse hover to know what exactly went wrong or right.
 7. Ability to run as a Windows service.

#### Windows service ####
This app can install itself as a Windows service. This service sits in background and monitors configured directories for any PNG files being added. When a file is added, it's automatically processed with OptiPNG.

To (un)install this service you need to run the app as administrator and click the button in settings menu. The service will reload settings when they're changed in the main app so you don't need to manually re-start it after adding some folders or changing other parameters.

This feature is highly experimental.
 
#### Other things ####

I just wanted to play with WPF and [MahApps.Metro](http://mahapps.com) a bit and happened to need an OptiPNG-only GUI. You probably shouldn't use this code for learning.

.NET 4.5 is required, which means it won't run on Windows XP. Would look quite weird anyway.

The name is terrible because I'm terrible at naming.