# Icon Browser

A plugin for Godot 4.5 to easily browse and search FontAwesome 7 icons, change their fill colour and save them to a directory.

## KNOWN ISSUE

The plugin downloads the icons and attempts to populate the dock before they have been imported as Resources by Godot, due to the fact that EditorFileSystem.Scan() merely tells Godot to scan the filesystem for changes, it does not block until the scan has completed. The solution is just to disable and re-enable the plugin on first load of the plugin, and whenever manually refreshing the cache.
