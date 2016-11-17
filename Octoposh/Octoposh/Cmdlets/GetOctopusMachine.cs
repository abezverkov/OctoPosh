﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using Octoposh.Model;
using Octopus.Client.Model;

namespace Octoposh.Cmdlets
{
    [Cmdlet("Get", "OctopusMachine", DefaultParameterSetName = All)]
    [OutputType(typeof(List<OutputOctopusMachine>))]
    [OutputType(typeof(List<MachineResource>))]
    public class GetOctopusMachine : PSCmdlet
    {
        private const string ByName = "ByName";
        private const string ByEnvironment = "ByEnvironment";
        private const string All = "All";
        private const string ByCommunicationStyle = "ByCommunicationStyle";
        private const string ByURL = "ByURL";
        

        [Alias("Name")]
        [Parameter(Position = 1, ValueFromPipeline = true, ParameterSetName = ByName)]
        public List<string> MachineName { get; set; }

        [Alias("Environment")]
        [Parameter(ValueFromPipeline = true, ParameterSetName = ByEnvironment)]
        public List<string> EnvironmentName { get; set; }

        [Alias("URI")]
        [Parameter(ValueFromPipeline = true, ParameterSetName = ByURL)]
        public List<string> URL { get; set; }

        [Alias("Mode")]
        [ValidateSet("Listening", "Polling", "SSH", "CloudRegion", "OfflineDrop")]
        [Parameter(ValueFromPipeline = true, ParameterSetName = ByCommunicationStyle)]
        public string CommunicationStyle { get; set; }

        [Parameter]
        public SwitchParameter ResourceOnly {get; set; }

        private OctopusConnection _connection;

        protected override void BeginProcessing()
        {
            _connection = new NewOctopusConnection().Invoke<OctopusConnection>().ToList()[0];
            MachineName = MachineName?.ConvertAll(s => s.ToLower());
            EnvironmentName = EnvironmentName?.ConvertAll(s => s.ToLower());
        }

        protected override void ProcessRecord()
        {
            var baseResourceList = new List<MachineResource>();

            switch (this.ParameterSetName)
            {
                case All:
                    baseResourceList = _connection.Repository.Machines.FindAll();
                    break;

                case ByName:
                    //Multiple values but one of them is wildcarded, which is not an accepted scenario (I.e -MachineName WebServer*, Database1)
                    if (MachineName.Any(item => WildcardPattern.ContainsWildcardCharacters(item) && MachineName.Count > 1))
                    {
                        throw OctoposhExceptions.CollectionHasRegularAndWildcardItem("MachineName");
                    }
                    //Only 1 wildcarded value (ie -MachineName WebServer*)
                    else if (MachineName.Any(item => WildcardPattern.ContainsWildcardCharacters(item) && MachineName.Count == 1))
                    {
                        var pattern = new WildcardPattern(MachineName[0].ToLower());
                        baseResourceList = _connection.Repository.Machines.FindMany(x => pattern.IsMatch(x.Name.ToLower()));
                    }
                    //multiple non-wildcared values (i.e. -MachineName WebServer1,Database1)
                    else if (!MachineName.Any(item => WildcardPattern.ContainsWildcardCharacters(item)))
                    {
                        baseResourceList = _connection.Repository.Machines.FindMany(x => MachineName.Contains(x.Name.ToLower()));
                    }
                    break;
                case ByCommunicationStyle:

                    var endpointtype = "";
                        
                    switch (CommunicationStyle)
                    {
                        case "Polling":
                            endpointtype = "Octopus.Client.Model.Endpoints.PollingTentacleEndpointResource";
                            break;
                        case "Listening":
                            endpointtype = "Octopus.Client.Model.Endpoints.ListeningTentacleEndpointResource";
                            break;
                        case "CloudRegion":
                            endpointtype = "Octopus.Client.Model.Endpoints.CloudRegionEndpointResource";
                            break;
                        case "OfflineDrop":
                            endpointtype = "Octopus.Client.Model.Endpoints.OfflineDropEndpointResource";
                            break;
                        case "SSH":
                            endpointtype = "Octopus.Client.Model.Endpoints.SSHEndpointResource";
                            break;
                    }

                    baseResourceList = _connection.Repository.Machines.FindMany(x => x.Endpoint.GetType().ToString().ToLower() == endpointtype.ToLower());

                    break;

                case ByEnvironment:
                    foreach (var name in EnvironmentName)
                    {
                        var environment = _connection.Repository.Environments.FindByName(name);
                        baseResourceList.AddRange(_connection.Repository.Environments.GetMachines(environment));
                    }
                    break;

            }

            if (ResourceOnly)
            {
                WriteObject(baseResourceList);
            }
            else
            {
                var converter = new OutputConverter();
                var outputList = converter.GetOctopusMachine(baseResourceList);

                WriteObject(outputList);
            }
            
        }
    }
}
