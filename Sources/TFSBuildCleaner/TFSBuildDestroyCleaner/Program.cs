using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using CommandLine;
using CommandLine.Text;
using Microsoft.TeamFoundation.Build.Client;
using Microsoft.TeamFoundation.Client;


namespace TFSBuildDestroyCleaner
{
    /// <summary>
    /// pour les parametres voir la pakage Commandline : http://commandline.codeplex.com/
    /// </summary>

    class Program
    {
        static void Main(string[] args)
        {

            var options = new Options();
            if (CommandLine.Parser.Default.ParseArguments(args, options))
            {

                // URI of the team project collection
                string _myUri = options.TfsUrlCollection;
                string TeamProject = options.TeampProject;
                string BuildDefinition = options.BuildDefinition;
                bool force = options.Force;


                var teamProjectCollection = new TfsTeamProjectCollection(new Uri(_myUri));
                //build definition liste for the collection
                var _bs = teamProjectCollection.GetService<IBuildServer>();

                // Print the name of the team project collection
                Console.WriteLine("Tool for clean deleted builds for team project : " + BuildDefinition);

                string conf = "Y";

                if (!force) //if not parameter -force
                {
                    Console.WriteLine("You are sur to delete all ?(Y/N)");
                    conf = Console.ReadLine();
                }
                if (conf.ToUpper() == "Y")
                {
                    var buildsDef = _bs.QueryBuildDefinitions(TeamProject); //Team project

                    if (BuildDefinition != "*")
                    {
                        var buildDefinition = buildsDef.FirstOrDefault(p => p.Name == BuildDefinition);
                        CleanBuildDefinition(args, buildDefinition, _bs, TeamProject, teamProjectCollection, options);
                    }
                    
                    else
                    {
                        foreach (var buildDefinition in buildsDef)
                        {
                            CleanBuildDefinition(args, buildDefinition, _bs, TeamProject, teamProjectCollection, options);
                        }
                    }
                }
                else
                {
                    Console.WriteLine("Cancel cleaning " + BuildDefinition + " ok");
                }


            }
            //Console.ReadLine();
        }

        private static void CleanBuildDefinition(string[] args, IBuildDefinition buildDefinition,
                                                     IBuildServer _bs, string TeamProject,
                                                     TfsTeamProjectCollection teamProjectCollection, Options options)
        {
            int nbTotal = RetourneNbBuildDef(_bs, TeamProject, buildDefinition, 10000).Count();

            Console.WriteLine(nbTotal + " Builds destroying");
            int nb = 0;
            do
            {
                var builds = RetourneNbBuildDef(_bs, TeamProject, buildDefinition, 100);
                nb = builds.Count();

                foreach (var buildDetail in builds)
                {
                    Console.WriteLine(String.Format("Cleaning of {0} / {1}", buildDefinition.Name,
                                                    buildDetail.BuildNumber));
                    DestroyBuild(teamProjectCollection.Uri.ToString(), buildDefinition.Name,
                                   buildDetail.BuildNumber, options.TeampProject);
                }
            } while (nb > 0);

            Console.WriteLine("=====Cleaning of " + buildDefinition.Name + " done=========");
        }

        private static IBuildDetail[] RetourneNbBuildDef(IBuildServer _bs, string TeamProject, IBuildDefinition buildDefinition, int nbMax)
        {

            IBuildDetailSpec def = _bs.CreateBuildDetailSpec(TeamProject);
            // only bring back the last 100 deleted builds
            def.MaxBuildsPerDefinition = nbMax;
            // query for only deleted builds
            def.QueryDeletedOption = QueryDeletedOption.OnlyDeleted;
            // Last deleted should be returned 1st
            def.QueryOrder = BuildQueryOrder.FinishTimeDescending;
            // Only look for deleted builds in the chosen build definition
            def.DefinitionSpec.Name = buildDefinition.Name;
            // Bring back deleted builds from any state
            def.Status = BuildStatus.All;
            // Pass this query for processing to the build service
            IBuildDetail[] builds = _bs.QueryBuilds(def).Builds;
            return builds;
        }


        static void DestroyBuild(string collection, string builddef, string build, string teamProject)
        {
            string tfsbuildPath = System.Configuration.ConfigurationManager.AppSettings["TfsBuildPath"];
            string ExecutableFilePath =
                       string.Concat(tfsbuildPath,@"Common7\IDE\tfsbuild.exe");
            string Arguments =
                string.Format(
                    @"destroy /collection:{0} /builddefinition:""{1}"" ""{2}""",
                    collection, teamProject + "/" + builddef, build);


            //Create Process Start information
            var processStartInfo =
                new ProcessStartInfo(ExecutableFilePath, Arguments);
            processStartInfo.ErrorDialog = false;
            processStartInfo.UseShellExecute = false;
            processStartInfo.RedirectStandardError = true;
            processStartInfo.RedirectStandardInput = true;
            processStartInfo.RedirectStandardOutput = true;

            //Execute the process
            var process = new Process();
            process.StartInfo = processStartInfo;
            bool processStarted = process.Start();
            if (processStarted)
            {
                StreamReader errorReader = null;

                //Get the output stream
                errorReader = process.StandardError;
                process.WaitForExit();

                //Display the result
                string displayText = string.Empty;
                //if error display error output
                if (!string.IsNullOrEmpty(errorReader.ReadToEnd()))
                {
                    displayText += errorReader.ReadToEnd();
                    Console.WriteLine(displayText);
                }

            }
        }

    }


    class Options
    {

        [Option('t', "TeamProject", Required = true, HelpText = "Name of the TFS Team project")]
        public string TeampProject { get; set; }

        [Option('b', "BuildDefinition", Required = true, HelpText = "Name of the TFS Build Definition of Team project")]
        public string BuildDefinition { get; set; }

        [Option('c', "TFSUrlCollection", Required = true,HelpText = "Url of the TFS Collection")]
        public string TfsUrlCollection { get; set; }

        [Option('f', "Force", DefaultValue = false, HelpText = "Force the cleaner within the message confirmation")]
        public bool Force { get; set; }

        [ParserState]
        public IParserState LastParserState { get; set; }

        [HelpOption]
        public string GetUsage()
        {
            return HelpText.AutoBuild(this,
              (HelpText current) => HelpText.DefaultParsingErrorsHandler(this, current));
        }
    }
}
