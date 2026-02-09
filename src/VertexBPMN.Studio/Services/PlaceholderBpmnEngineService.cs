//using Microsoft.AspNetCore.Components.Forms;
//using System;
//using System.Collections.Generic;
//using System.Threading.Tasks;
//using System.Linq; // Added for FirstOrDefault and RemoveAll

//namespace VertexBPMN.Studio.Services
//{
//            public class PlaceholderBpmnEngineService : IBpmnEngineService
//        {
//            public event Action<string>? OnEventEmitted;
    
//            private readonly Dictionary<string, List<Deployment>> _deployments = new();
//            private readonly Dictionary<string, List<ProcessDefinition>> _processDefinitions = new();
//            private readonly Dictionary<string, List<ProcessInstance>> _processInstances = new();
//            private readonly Dictionary<string, List<UserTask>> _userTasks = new();
//            private readonly List<EngineConnection> _engineConnections = new()
//            {
//                new EngineConnection { Id = "engine1", Name = "Development Engine", Url = "http://localhost:8080", IsActive = true },
//                new EngineConnection { Id = "engine2", Name = "Staging Engine", Url = "http://localhost:8081", IsActive = false }
//            };
    
//            public async Task DeployAsync(IBrowserFile file)
//            {
//                await Task.Delay(2000); // Simulate network latency
    
//                if (!_deployments.ContainsKey("engine1"))
//                {
//                    _deployments["engine1"] = new List<Deployment>();
//                }
//                var newDeployment = new Deployment
//                {
//                    Id = Guid.NewGuid().ToString(),
//                    Name = file.Name,
//                    DeploymentTime = DateTime.Now
//                };
//                _deployments["engine1"].Add(newDeployment);
//                OnEventEmitted?.Invoke($"Deployment created: {newDeployment.Name} ({newDeployment.Id})");
//            }
//        public Task<IEnumerable<Deployment>> GetDeploymentsAsync()
//        {
//            if (!_deployments.ContainsKey("engine1"))
//            {
//                return Task.FromResult<IEnumerable<Deployment>>(new List<Deployment>());
//            }
//            return Task.FromResult<IEnumerable<Deployment>>(_deployments["engine1"]);
//        }

//        public Task<IEnumerable<ProcessDefinition>> GetProcessDefinitionsAsync()
//        {
//            if (!_processDefinitions.ContainsKey("engine1"))
//            {
//                _processDefinitions["engine1"] = new List<ProcessDefinition>
//                {
//                    new ProcessDefinition { Id = "process1", Key = "Process_1", Name = "My First Process", Version = 1 },
//                    new ProcessDefinition { Id = "process2", Key = "Process_2", Name = "My Second Process", Version = 1 },
//                    new ProcessDefinition { Id = "process2", Key = "Process_2", Name = "My Second Process", Version = 2 },
//                };
//            }
//            return Task.FromResult<IEnumerable<ProcessDefinition>>(_processDefinitions["engine1"]);
//        }

//        public Task<IEnumerable<ProcessInstance>> GetProcessInstancesAsync()
//        {
//            if (!_processInstances.ContainsKey("engine1"))
//            {
//                _processInstances["engine1"] = new List<ProcessInstance>
//                {
//                    new ProcessInstance { Id = "instance1", ProcessDefinitionId = "process1", StartTime = DateTime.Now.AddHours(-1), State = "Active" },
//                    new ProcessInstance { Id = "instance2", ProcessDefinitionId = "process2", StartTime = DateTime.Now.AddMinutes(-30), State = "Active" },
//                    new ProcessInstance { Id = "instance3", ProcessDefinitionId = "process2", StartTime = DateTime.Now.AddMinutes(-15), State = "Suspended" },
//                };
//            }
//            return Task.FromResult<IEnumerable<ProcessInstance>>(_processInstances["engine1"]);
//        }

//        public Task<IEnumerable<UserTask>> GetTasksAsync()
//        {
//            if (!_userTasks.ContainsKey("engine1"))
//            {
//                _userTasks["engine1"] = new List<UserTask>
//                {
//                    new UserTask { Id = "task1", Name = "Approve Invoice", ProcessInstanceId = "instance1", CreatedTime = DateTime.Now.AddMinutes(-10), Assignee = "John Doe", FormKey = "invoiceApprovalForm", DueDate = DateTime.Now.AddDays(7), Role = "Admin" },
//                    new UserTask { Id = "task2", Name = "Review Order", ProcessInstanceId = "instance2", CreatedTime = DateTime.Now.AddMinutes(-5), Assignee = "Jane Doe", DueDate = DateTime.Now.AddDays(-1), Role = "User" },
//                    new UserTask { Id = "task3", Name = "Ship Product", ProcessInstanceId = "instance2", CreatedTime = DateTime.Now.AddMinutes(-2), Assignee = "John Doe", Role = "Admin" },
//                };
//            }
//            return Task.FromResult<IEnumerable<UserTask>>(_userTasks["engine1"]);
//        }

//        public Task<string> GetProcessDefinitionXmlAsync(string processDefinitionId)
//        {
//            // Return a dummy BPMN XML for now
//            return Task.FromResult(@"<?xml version=""1.0"" encoding=""UTF-8""?>
//<bpmn:definitions xmlns:bpmn=""http://www.omg.org/spec/BPMN/20100524/MODEL"" id=""Definitions_1"" targetNamespace=""http://bpmn.io/schema/bpmn"">
//  <bpmn:process id=""Process_1"" isExecutable=""false"">
//    <bpmn:startEvent id=""StartEvent_1"" />
//    <bpmn:endEvent id=""EndEvent_1"" />
//  </bpmn:process>
//</bpmn:definitions>");
//        }

//        public Task<IEnumerable<ProcessDefinition>> GetProcessDefinitionVersionsAsync(string key)
//        {
//            // For simplicity, return the same dummy versions regardless of engineId for now
//            var versions = new List<ProcessDefinition>
//            {
//                new ProcessDefinition { Id = $"{key}-v1", Key = key, Name = $"Process {key} Version 1", Version = 1 },
//                new ProcessDefinition { Id = $"{key}-v2", Key = key, Name = $"Process {key} Version 2", Version = 2 },
//                new ProcessDefinition { Id = $"{key}-v3", Key = key, Name = $"Process {key} Version 3", Version = 3 },
//            };
//            return Task.FromResult<IEnumerable<ProcessDefinition>>(versions);
//        }

//        public async Task RollbackProcessDefinitionAsync(string engineId, string processDefinitionId)
//        {
//            await Task.Delay(1000); // Simulate rollback process
//            Console.WriteLine($"Simulating rollback for process definition: {processDefinitionId} on engine {engineId}");
//        }

//        private EngineConfiguration _currentConfig = new EngineConfiguration
//        {
//            StatusMessage = "Engine is running normally.",
//            DeploymentDelayMs = 2000
//        };

//        public Task<EngineConfiguration> GetEngineConfigurationAsync()
//        {
//            return Task.FromResult(_currentConfig);
//        }

//        public async Task UpdateEngineConfigurationAsync(EngineConfiguration configuration)
//        {
//            await Task.Delay(500); // Simulate saving configuration
//            _currentConfig = configuration;
//            Console.WriteLine($"Configuration updated: StatusMessage='{_currentConfig.StatusMessage}', DeploymentDelayMs='{_currentConfig.DeploymentDelayMs}'");
//        }
//        public Task<IEnumerable<EngineConnection>> GetEngineConnectionsAsync()
//        {
//            return Task.FromResult<IEnumerable<EngineConnection>>(_engineConnections);
//        }

//        public async Task AddEngineConnectionAsync(EngineConnection connection)
//        {
//            await Task.Delay(500);
//            connection.Id = Guid.NewGuid().ToString();
//            _engineConnections.Add(connection);
//        }

//        public async Task UpdateEngineConnectionAsync(EngineConnection connection)
//        {
//            await Task.Delay(500);
//            var existingConnection = _engineConnections.FirstOrDefault(c => c.Id == connection.Id);
//            if (existingConnection != null)
//            {
//                existingConnection.Name = connection.Name;
//                existingConnection.Url = connection.Url;
//                existingConnection.IsActive = connection.IsActive;
//            }
//        }

//        public async Task RemoveEngineConnectionAsync(string connectionId)
//        {
//            await Task.Delay(500);
//            _engineConnections.RemoveAll(c => c.Id == connectionId);
//        }
//    }
//}