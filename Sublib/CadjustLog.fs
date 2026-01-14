namespace Sublib

open System
open System.IO
open System.Text
open System.Text.RegularExpressions
open System.Collections.Generic
open FSharp.Stats
open FSharp.Stats.Distributions.Continuous

module Cadjust =

    /// cap cadjust measurement array
    type MeasCaps = double array
    
    /// mean, stddev value from log
    type StatData = {
        mean: double
        stdDev: double
    }

    type MasterData = Dictionary<double, StatData> array
   
    /// handle data
    type LogData = {
        expectedCap: double
        zCoord: double
        /// (z * meascaps) array [head]
        measArr: (double * MeasCaps) array array
        /// (z * statdata) array [head]
        statsArr: (double * StatData) array array
        /// cadjust factor arr
        factors: double array
        /// cadjust zcorrection arr
        zCorrs : double array
        masterDataOpt: MasterData option
    }

    exception FactCheckErr of string

    let checkRangeForZVal = 1.5, 2.6
    
    let headStrToInt (headstr: string): int =
        let add =
            match headstr[1] with
            | 'L' -> 0
            | _ -> 1
        let r = Char.GetNumericValue headstr[0] |> int
        r * 2 + add
            
    let indexByHeadNum headnum =
        match headnum with
        | 1 -> 0
        | 2 -> 1
        | 4 | 8 -> 2
        | _ -> 3
        
    let headNumByIndex index =
        match index with
        | 0 -> 1
        | 1 -> 2
        | 2 -> 4
        | _ -> 7
        
    let saveMasterCadjustFile(d: LogData) (newpath: string) =
        use bw = File.CreateText newpath
        // save factor
        bw.WriteLine($"//factor:{d.factors[0]},{d.factors[1]},{d.factors[2]},{d.factors[3]}")
        for i = 0 to 3 do
            let headnum = headNumByIndex i
            let data = d.statsArr[i]
            for (z, ms) in data do
                bw.WriteLine($"{headnum},%.1f{z},%.3f{ms.mean},%.3f{ms.stdDev}")
        bw.Flush()
        bw.Close()

    /// load cadjust master file
    /// ret : MasterData(c), factor array
    let loadCadjustMasterFile(mfile: string): (MasterData * double array)  = 
        let br = File.ReadLines(mfile)
        let factors = Array.zeroCreate<double> 4
        let data = 
            [| for l in br do
                match l with
                | "" -> ()
                | a when a.StartsWith("//factor:") ->
                    let b = a.Replace("//factor:", "").Trim().Split([|','|]) |> Array.map double
                    for i = 0 to 3 do
                        factors[i] <- b[i]
                    //for
                | _ ->
                    let b = l.Split([|","|], StringSplitOptions.RemoveEmptyEntries)
                    if b.Length <> 4 then 
                        failwith $"master file is not correct format line {l}"
                    let h = int b[0]
                    let z = double b[1]
                    let v = double b[2]
                    let std = double b[3]
                    yield {| head = h; z = z; value = v; stddev = std |} |]
        let result: MasterData = Array.init 4 (fun _ -> Dictionary<double, StatData>())
        for d in data do
            let index = indexByHeadNum d.head
            // if overlap z value, overwrite
            result[index][d.z] <- {mean = d.value; stdDev = d.stddev}
        result, factors

    /// read log file, either cadjust or verify...
    /// ret: LogData
    let loadLogFile(logfile: string): LogData =
        let br = File.ReadLines(logfile)
        
        /// regex variables
        let headReg = Regex("""head ([\dLR]+)""")
        let cReg = Regex("""([\d.]+) pF""")
        let zReg1 = Regex("""Z\s+Mean""")
        let zReg2 = Regex("""Z\s+1\s+2""")
        let factorReg = Regex("""Head\s+([\dLR]+):\s+Factor\s+([\d.]+).+Z corr\s+([\d.\-]+)""")

        /// throw exception possible
        let parseHead(s: string): int = 
            let m = headReg.Match(s)
            if m.Success |> not then failwithf "%s can not parse" s
            m.Groups[1].Value |> headStrToInt

        let mutable curHeadNum = -1
        let mutable readOk = false
        /// expected capacity
        let mutable cap = 0.0
        /// peak z coordinate
        let mutable peakZCor = 0.0
        let mutable isCadjustLog = false
        let mutable fileFinished = false
        
        let factors = Array.zeroCreate<double> 4
        let zCorrs = Array.zeroCreate<double> 4

        /// (z, meascap)
        let meass = Array.init 4 (fun _ -> ResizeArray<double * MeasCaps>())
        /// (z, statdata)
        let stats = Array.init 4 (fun _ -> ResizeArray<double * StatData>())        
        
        for l in br do
            match l.Trim() with
            | a when a = "" -> ()
            
            // get cap value
            | a when a.StartsWith("Expected") -> 
                let m = cReg.Match(a)
                if m.Success then
                    cap <- Convert.ToDouble(m.Groups[1].Value) |> double 
                else
                    failwithf "%s can not parse" a
            
            // beginning
            | a when a.StartsWith("BACKDRILL MEAS") -> readOk <- false
            
            // head number parsing for Verify
            | a when a.StartsWith("Verification for") ->                
                curHeadNum <- parseHead a
                isCadjustLog <- false
                
            // head number parsing for cadjust log
            | a when a.StartsWith("Mean measurements") ->
                curHeadNum <- parseHead a
                isCadjustLog <- true
                
            | a when a.StartsWith("Z") ->
                 let zr = if isCadjustLog then zReg1 else zReg2
                 if zr.IsMatch(a) then readOk <- true else ()

            | a when a.StartsWith("Head ") && a.Contains("Factor") ->
                let m = factorReg.Match(a)
                if m.Success then
                    let hIndex = m.Groups[1].Value |> headStrToInt |> indexByHeadNum
                    let f = double <| m.Groups[2].Value
                    let zc = double <| m.Groups[3].Value
                    factors[hIndex] <- f
                    zCorrs[hIndex] <- zc

            // end of readling parse factor lines
            | a when readOk && (a.StartsWith("Measurements") || a.StartsWith("-----")) -> 
                readOk <- false
            | a when a.StartsWith("Finish") ->
                readOk <- false
                fileFinished <- true
           
            // data value line handling   
            | a when readOk && cap > 0 ->
                let b = a.Split([|" "|], StringSplitOptions.RemoveEmptyEntries)
                // check line is data?
                try
                    let bvals = b |> Array.map double
                    let index = indexByHeadNum curHeadNum
                    if bvals.Length = 3 then
                        stats[index].Add(bvals[0], {mean = bvals[1]; stdDev = bvals[2]})
                    else
                        let caps = bvals[1..10]
                        meass[index].Add(bvals[0], caps)
                        let meanv = Seq.average caps
                        let stdv = Seq.stDev caps
                        stats[index].Add(bvals[0], {mean = meanv; stdDev = stdv})
                with
                | :? Exception as e -> readOk <- false
                
            | _ -> ()
        // for

        if fileFinished then
            let result = 
                {
                    LogData.expectedCap = cap
                    zCoord = peakZCor 
                    measArr = meass |> Array.map(fun a -> a |> Seq.toArray)
                    statsArr = stats |> Array.map(fun a -> a |> Seq.toArray)
                    factors = factors
                    zCorrs = zCorrs
                    masterDataOpt = None
                }
            
            let masterpath = 
                let pp = Path.GetDirectoryName logfile
                Path.Combine(pp, "cadjust-master.txt")

            if result.measArr[0].Length = 0 then            
                // is cajdust
                saveMasterCadjustFile result masterpath 
                result 
            else
                // is verify
                if File.Exists masterpath then
                    let masterarr, facts = loadCadjustMasterFile masterpath
                    { result with masterDataOpt = Some masterarr; factors = facts }
                else
                    result
            // if
        else
            // something wrong.. 
            { expectedCap = 0.0; zCoord = 0.0; measArr = Array.empty; statsArr = Array.empty; factors = Array.empty; zCorrs = Array.empty; masterDataOpt = None }
    // let loadLogFile
   
    /// ret: median, MAD (median absolute deviation) 
    let computeFactorStat (d: double array) =
        let m = Seq.median d 
        let mad = d |> Seq.map(fun v -> abs(v - m)) |> Seq.median
        let min1, max1 = Seq.min d, Seq.max d
        let width = max1 - min1
        let variation = width / m * 100.0
        // increasing 40%
        if variation > 40.0 then 0.0, 0.0 else m, mad
        
    /// ret : average, stddev for ZCorr
    let computeZCorrStat (d: LogData) =
        Seq.average d.zCorrs, Seq.stDev d.zCorrs
        
    /// ret: ok or fail, errmsg
    let evaluateCVForOneData (d: (double * StatData)) headNum cvCheck =
        let { mean = avg; stdDev = stdv }  = snd d
        let z = fst d
        // skip if checkRangeForZVal outof range
        if z > snd checkRangeForZVal || z < fst checkRangeForZVal then true, ""
        else
            let zVal = $"%.1f{z}"
            let cv = stdv / avg * 100.0
            if cv > cvCheck then
                false, $"head:{headNum} z:{zVal} cv:%.1f{cv}%%"
            else
                true, ""
            
    let evaluateCVForStatArr (d: (double * StatData) array) headNum cvCheck =
        let sb = StringBuilder()
        for i in d do
            let r, v = evaluateCVForOneData i headNum cvCheck
            if not r then
                sb.AppendLine(v) |> ignore
        //
        sb.ToString()
            
    /// ret : ok/false, (lsl, usl), errmsg
    let evaluateFactorRange (d: double array) sigmaval =
        let a, b = computeFactorStat d       
        if a = 0.0 || b = 0.0 then 
            let erdata = String.Join(",", d)
            raise <| FactCheckErr($"Factor variation too large : {erdata}")
        // LSL check added 50% more..
        let lslv = 
            let k = a - sigmaval * 1.5 * b
            if k < 0.0 then 0.0 else k
        let uslv = a + sigmaval * b
        // factor value small is fine ==> Sensor head is bigger..
        let fails = d |> Array.indexed |> Array.filter(fun (_,v) -> lslv < v && v > uslv)        
        if fails.Length = 0 then
            true, (lslv, uslv), ""
        else
            let heads = fails |> Array.map(fun (i,_) -> headNumByIndex i)
            let mm = String.Join(",", heads)
            false, (lslv, uslv), $"Factor failed: Heads {mm}"
    // evaluateFactorRange

    let private checkAccuracyCValue(d: LogData) ckRef = 
        if d.masterDataOpt.IsNone then failwith $"error no master data loaded"
        let sb = StringBuilder()
        let headResults = [|true; true; true; true|]        
        let minZ, maxZ = checkRangeForZVal
        for i = 0 to 3 do
            let headnum = headNumByIndex i
            sb.AppendLine($"Head: {headnum}") |> ignore
            let ddata = d.measArr[i] |> Array.filter(fun (z, v) -> minZ <= z && z <= maxZ)            
            let cdata = d.statsArr[i] |> Array.filter(fun (z,v) -> minZ <= z && z <= maxZ)
            let zarr = snd ddata[0] |> Array.indexed |> Array.map(fun g -> fst g + 1)
            sb.AppendLine("Z,REF,"+String.Join(",", zarr)+",Mean,StdDev,LSL,USL,Result") |> ignore
            let mdata = d.masterDataOpt.Value[i]
            let fails = 
                Array.zip ddata cdata
                |> Array.choose(fun ((z1,v1), (z2,v2)) ->
                    let measStr = String.Join(",", v1 |> Array.map(fun a -> sprintf "%.3f" a))
                    let refValue = mdata[z1].mean
                    let minC = refValue - ckRef * refValue
                    let maxC = refValue + ckRef * refValue
                    let resStr = 
                        let ck = v1 |> Array.exists(fun g -> minC > g || g > maxC)
                        if ck then "NG" else "OK"
                    sb.AppendLine(sprintf "%.1f,%.3f,%s,%.3f,%.3f,%.3f,%.3f,%s" z1 refValue measStr v2.mean v2.stdDev minC maxC resStr) |> ignore
                    if resStr = "NG" then Some z1 else None
                )
            if fails.Length = 0 then
                headResults[i] <- true
            else
                headResults[i] <- false
            sb.AppendLine("-----------------------------------------------------------------------------------------") |> ignore 
        // for
        headResults, sb
    // checkAccuracyCValue

    /// evaluate is for verify mode
    /// ret: 3steps check result, msg
    let evaluateLogData (d: LogData) cvCheck sigmaval accvalper =
        let checkResult = [|true; true; true|]
        let msg = StringBuilder()

        // check1
        msg.AppendLine($"Check Measurements Coefficient variation (%.1f{cvCheck}%%):") |> ignore
        let rstr = 
            [|
                evaluateCVForStatArr d.statsArr[0] (headNumByIndex 0) cvCheck
                evaluateCVForStatArr d.statsArr[1] (headNumByIndex 1) cvCheck
                evaluateCVForStatArr d.statsArr[2] (headNumByIndex 2) cvCheck
                evaluateCVForStatArr d.statsArr[3] (headNumByIndex 3) cvCheck
            |] 
            |> Array.choose(fun a ->
                let b = a.Trim()
                if b <> "" then Some b else None)

        let statCheckResult = String.Join("\r\n---------------------\r\n", rstr)        

        if statCheckResult.Trim() = "" then
            msg.AppendLine("Okay") |> ignore
        else
            checkResult[0] <- false
            msg.AppendLine(statCheckResult) |> ignore
        
        // check2
        msg.AppendLine().Append("Needle Sensor range check ") |> ignore
        try
            let c,dd,e = evaluateFactorRange d.factors sigmaval
            msg.AppendLine($"%.3f{fst dd} - %.3f{snd dd}:") |> ignore
            if c then msg.AppendLine("Okay") |> ignore
            else 
                msg.AppendLine(e) |> ignore
                checkResult[1] <- false
        with
        | FactCheckErr er -> 
            checkResult[1] <- false
            msg.AppendLine(er + "\nRange check failed") |> ignore
        
        // verify checking
        if d.masterDataOpt.IsSome then
            // check3 : accuracy cvalue (3%)
            msg.AppendLine().AppendLine($"Accuracy check %.0f{accvalper}%% of Ref Value") |> ignore
            let ck3, ck3msg = checkAccuracyCValue d (accvalper / 100.0)
            checkResult[2] <- ck3 |> Array.forall(fun a -> a = true)
            msg.AppendLine(ck3msg.ToString()) |> ignore
            ck3msg.Clear() |> ignore
        // if
        checkResult, msg.ToString()
    //evaluateLogData
