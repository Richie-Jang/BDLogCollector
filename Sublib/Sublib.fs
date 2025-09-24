namespace Sublib

open System.IO

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

    let SaveIniFile(appPath: string, section: string, key: string, value: string): unit =
        let iniPath = Path.Combine(appPath, "logger.ini")
        let parser = IniParser.FileIniDataParser()
        if File.Exists(iniPath) = false then
            parser.WriteFile(iniPath, IniParser.Model.IniData())

        let data = parser.ReadFile(iniPath)
        data[section][key] <- value
        parser.WriteFile(iniPath, data)
