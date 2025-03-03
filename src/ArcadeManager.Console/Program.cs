﻿using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using ArcadeManager.Console.Commands;
using ArcadeManager.Console.Settings;
using Spectre.Console;
using Spectre.Console.Cli;

namespace ArcadeManager.Console;

public static class Program
{
    public static int Main(string[] args)
    {
        System.Console.ReadLine();

        var app = new CommandApp();

        app.Configure(config =>
        {
            config.AddBranch<CsvSettings>("csv", csv =>
            {
                csv.AddCommand<CsvAddCommand>("add");
                csv.AddCommand<CsvDeleteCommand>("delete");
                csv.AddCommand<CsvKeepCommand>("keep");
                csv.AddCommand<CsvConvertDatCommand>("convertdat");
                csv.AddCommand<CsvConvertIniCommand>("convertini");
            });

            config.AddBranch<RomsSettings>("roms", roms => {
                roms.AddCommand<RomsAddCommand>("add");
                roms.AddCommand<RomsDeleteCommand>("delete");
                roms.AddCommand<RomsKeepCommand>("keep");
            });

            config.AddCommand<RomsCheckDatCommand>("checkdat");
        });

        return app.Run(args);
    }
}
