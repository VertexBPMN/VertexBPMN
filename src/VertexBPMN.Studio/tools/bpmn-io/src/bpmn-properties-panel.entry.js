import {
  BpmnPropertiesPanelModule,
  BpmnPropertiesProviderModule
} from 'bpmn-js-properties-panel';
import vertexModdle from './vertex.json';
import VertexPropertiesProviderModule from './vertex-properties-provider.js';
import VertexValidationModule from './vertex-validation.js';

window.BpmnJSPropertiesPanelModule = BpmnPropertiesPanelModule;
window.BpmnJSPropertiesProviderModule = BpmnPropertiesProviderModule;
window.VertexBpmnModdle = vertexModdle;
window.VertexPropertiesProviderModule = VertexPropertiesProviderModule;
window.VertexValidationModule = VertexValidationModule;
