// Copyright 2020 Wason Technology, LLC
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//    http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.


using Mono.Options;
using System;
using System.Collections.Generic;

namespace RobotRaconteurLocalTapRecorder
{
    class Program
    {
        static void Main(string[] args)
        {
            bool shouldShowHelp = false;
            string tap_name = null;
            string output_dir = ".";
            bool log_record_only = false;
            var options = new OptionSet
            {
                {"tap-name=", "the name of the tap to record", n => tap_name = n },
                {"output-dir=", "the directory to save tap files", n => output_dir = n },
                {"log-record-only",  "only save log records, not message traffic", n => log_record_only = n != null},
                {"h|help", "show this message and exit", h=> shouldShowHelp = h != null }
            };

            List<string> extra;
            try
            {
                // parse the command line
                extra = options.Parse(args);
            }
            catch (OptionException e)
            {
                // output some error message
                Console.Write("RobotRaconteurLocalTapRecorder: ");
                Console.WriteLine(e.Message);
                Console.WriteLine("Try `greet --help' for more information.");
                return;
            }

            using (var l = new LocalTapRecorder(output_dir, log_record_only, tap_name))
            {
                l.Start();
                Console.WriteLine("Press enter to exit");
                Console.ReadLine();
            }
        }

        static void ShowHelp(OptionSet options)
        {
            Console.WriteLine("Usage: RobotRaconteurLocalTapRecorder [OPTIONS]");
            Console.WriteLine("Record messages from Robot Raconteur taps to file");
            Console.WriteLine("If no file path is specified, current directory is used");
            Console.WriteLine();
            Console.WriteLine("Options:");
            options.WriteOptionDescriptions(Console.Out);            
        }
    }
}
