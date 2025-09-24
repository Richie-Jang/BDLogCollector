open System
open Avalonia
//open Avalonia.Logging.Serilog
open CalibTracer

[<CompiledName "BuildAvaloniaApp">] 
let buildAvaloniaApp () = 
    AppBuilder.Configure<App>()
              .UsePlatformDetect()
              .LogToTrace(areas = [||])

[<STAThread>][<EntryPoint>]
let main argv =
    buildAvaloniaApp().StartWithClassicDesktopLifetime(argv)
