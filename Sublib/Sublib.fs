namespace Sublib

open System.IO
open System

module FindPath =
    
    let GetTP2(): string = 
        let p1 = """c:\apps\atg\testplayer2"""
        let p2 = """c:\program files\atg\testplayer2"""
        let p3 = """c:\program files (x86)\atg\testplayer2"""
        
        if Directory.Exists(p1) then p1
        elif Directory.Exists(p2) then p2
        elif Directory.Exists(p3) then p3
        else ""
        
module IniFile = 

    type OllySet = {
        peakZ: float
        expectedC: float
        frequency: float
    } with
        member x.ToStr() =
            let peakCStr = $"%.1f{x.expectedC}fF"
            $"{peakCStr}_%.2f{x.peakZ}mm_%.0f{x.frequency / 1000.0}KHz"

    let SaveIniFile(appPath: string, section: string, key: string, value: string): unit =
        let iniPath = Path.Combine(appPath, "logger.ini")
        let parser = IniParser.FileIniDataParser()
        if File.Exists(iniPath) = false then
            parser.WriteFile(iniPath, IniParser.Model.IniData())

        let data = parser.ReadFile(iniPath)
        data[section][key] <- value
        parser.WriteFile(iniPath, data)

    let GetIniFile(appPath: string, section: string, key: string): string = 
        let iniPath = Path.Combine(appPath, "logger.ini")
        let parser = IniParser.FileIniDataParser()
        if File.Exists(iniPath) = false then
            ""
        else
            let data = parser.ReadFile(iniPath)
            if data[section].ContainsKey(key) then data[section][key] else ""

    let GetBackDrillSetFromOllyTestIni(): OllySet option =
        let ollySetIniPath = @"C:\windows\ollytest.ini"
        
        let loadFile(): OllySet option =
            let line = 
                File.ReadLines(ollySetIniPath)
                |> Seq.tryFind(fun s ->
                    s.Contains("Backdrill capacity")
                )
            if line.IsNone then None 
            else
                let l1 = line.Value.Split('=')
                if l1.Length <= 1 || l1[1] = "" then None 
                else
                    let l2 = l1[1].Split(',')
                    let mutable peakZ = 0.0
                    let mutable expectedC = 0.0
                    let mutable freq = 0.0
                    let mutable freqIndex = 1
                    if l2.Length = 4 then
                        Int32.TryParse(l2[3], &freqIndex) |> ignore
                    elif l2.Length >= 2 then
                        Double.TryParse(l2[0], &expectedC) |> ignore
                        Double.TryParse(l2[1], &peakZ) |> ignore
                    match freqIndex with
                    | 0 -> freq <- 2.0e+3 
                    | 2 -> freq <- 8.0e+3
                    | 3 -> freq <- 1.6e+4
                    | _ -> freq <- 4.0e+3
                    Some {  peakZ = peakZ; expectedC = expectedC; frequency = freq  }
        
        if File.Exists(ollySetIniPath) = false then
            None
        else            
            loadFile()            


    let GetVerifyCheckSetting (appPath: string) : (double * double * double) =
        let mutable ck1 = 5.0
        let mutable ck2 = 6.0
        let mutable ck3 = 5.0
        let a1 = GetIniFile(appPath, "verify", "ck1_cv")
        if a1 <> "" then 
            Double.TryParse(a1, &ck1) |> ignore
        let a2 = GetIniFile(appPath, "verify", "ck2_needlesensor");
        if a2 <> "" then
            Double.TryParse(a2, &ck2) |> ignore
        let a3 = GetIniFile(appPath, "verify", "ck3_accuracy");
        if a3 <> "" then
            Double.TryParse(a3, &ck3) |> ignore
        // update ini file
        SaveIniFile(appPath, "verify", "ck1_cv", ck1.ToString("F1"))
        SaveIniFile(appPath, "verify", "ck2_needlesensor", ck2.ToString("F1"))
        SaveIniFile(appPath, "verify", "ck3_accuracy", ck3.ToString("F1"))
        (ck1, ck2, ck3)

