using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

// What the Movement Lab found out. For each character, what the formula says it should do next to what it really did when the
// real game physics ran it. A value of -1 was not measured.
[Serializable]
public class LabReport
{
    public string when;
    public float bankHeight;
    public List<LabEntry> entries = new List<LabEntry>();

    public static string FilePath
    {
        get { return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "MovementLab.json")); }
    }

    public static string CsvPath
    {
        get { return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "MovementLab.csv")); }
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
        File.WriteAllText(FilePath, JsonUtility.ToJson(this, true));
        File.WriteAllText(CsvPath, ToCsv());
    }

    public static LabReport Load()
    {
        try
        {
            return File.Exists(FilePath) ? JsonUtility.FromJson<LabReport>(File.ReadAllText(FilePath)) : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    // One row for each thing measured, so it can be sorted and charted in a spreadsheet.
    public string ToCsv()
    {
        var text = new StringBuilder("character,pass,mode,metric,expected,measured\n");

        foreach (var e in entries)
        {
            void Row(string metric, float expected, float measured)
            {
                text.Append(Quote(e.character)).Append(',').Append(e.pass).Append(',').Append(e.mode).Append(',')
                    .Append(metric).Append(',').Append(Number(expected)).Append(',').Append(Number(measured)).Append('\n');
            }

            Row("top speed", e.expectedTopSpeed, e.topSpeed);
            Row("time to top speed", e.expectedTimeToTop, e.timeToTop);
            Row("stopping distance", e.expectedStopDistance, e.stopDistance);
            Row("jump height", e.expectedJumpHeight, e.jumpHeight);
            Row("tap jump height", -1f, e.tapHeight);
            Row("jump distance at speed", e.expectedJumpDistance, e.jumpDistance);
            Row("terminal speed", e.expectedTerminalSpeed, e.terminalSpeed);

            foreach (var f in e.fluids)
            {
                Row($"{f.fluid}: fraction under", f.expectedUnder, f.under);
                Row($"{f.fluid}: bobs", -1f, f.bobs);
                Row($"{f.fluid}: settle seconds", -1f, f.settleSeconds);
                Row($"{f.fluid}: sink speed", f.expectedSinkSpeed, f.sinkSpeed);
                Row($"{f.fluid}: kick rise", f.expectedKick, f.kickRise);
                Row($"{f.fluid}: escape seconds", -1f, f.escaped ? f.escapeSeconds : -1f);
            }
        }

        return text.ToString();
    }

    private static string Number(float value)
    {
        return value.ToString("0.###", CultureInfo.InvariantCulture);
    }

    private static string Quote(string text)
    {
        return "\"" + text.Replace("\"", "\"\"") + "\"";
    }
}

[Serializable]
public class LabEntry
{
    public string character;

    // "base", or "action" for a character tested with its action button held.
    public string pass;

    // The mode of its profile it was moving in.
    public string mode;
    public string material;
    public float mass;
    public float density;

    // False if it never moved sideways: it can't walk, or something kept it from.
    public bool moved = true;

    public float expectedTopSpeed = -1f;
    public float topSpeed = -1f;
    public float expectedTimeToTop = -1f;
    public float timeToTop = -1f;
    public float expectedStopDistance = -1f;
    public float stopDistance = -1f;
    public float stopSeconds = -1f;
    public float expectedJumpHeight = -1f;
    public float jumpHeight = -1f;
    public float tapHeight = -1f;
    public float apexSeconds = -1f;
    public float airSeconds = -1f;
    public float expectedJumpDistance = -1f;
    public float jumpDistance = -1f;
    public float expectedTerminalSpeed = -1f;
    public float terminalSpeed = -1f;
    public float fallSeconds = -1f;

    public List<LabFluidResult> fluids = new List<LabFluidResult>();
}

[Serializable]
public class LabFluidResult
{
    public string fluid;
    public float density;
    public float viscosity;

    public bool expectedFloats;
    public bool floats;

    // How much of it was under at rest: from 0 to 1, where 1 is all of it. For something that sinks it's 1.
    public float expectedUnder = -1f;
    public float under = -1f;

    // How many times it bobbed up after going in, and how long until it was still (for a floater), or how long until it
    // reached the bottom (for something that sinks).
    public int bobs;
    public float settleSeconds = -1f;

    public float expectedSinkSpeed = -1f;
    public float sinkSpeed = -1f;

    // How far one kick lifted it from rest.
    public float expectedKick = -1f;
    public float kickRise = -1f;

    // Whether, kicking and steering toward a bank, it got out onto it, and how long that took.
    public bool escaped;
    public float escapeSeconds = -1f;
}
