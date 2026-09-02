import {
  BpmnPropertiesPanelModule,
  BpmnPropertiesProviderModule
} from 'bpmn-js-properties-panel';
import BpmnModeler from 'bpmn-js/lib/Modeler';
import vertexModdle from './vertex.json';
import VertexPropertiesProviderModule from './vertex-properties-provider.js';
import VertexValidationModule from './vertex-validation.js';

window.BpmnJS = BpmnModeler;
window.BpmnModeler = BpmnModeler;
window.BpmnJSPropertiesPanelModule = BpmnPropertiesPanelModule;
window.BpmnJSPropertiesProviderModule = BpmnPropertiesProviderModule;
window.VertexBpmnModdle = vertexModdle;
window.VertexPropertiesProviderModule = VertexPropertiesProviderModule;
window.VertexValidationModule = VertexValidationModule;
