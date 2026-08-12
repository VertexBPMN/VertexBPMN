window.BpmnModelerInterop = {
    createModeler: function (containerId, bpmnXml) {
        const modeler = new BpmnJS({
            container: `#${containerId}`
        });

        modeler.importXML(bpmnXml)
            .then(function (result) {
                const { warnings } = result;
                console.log('rendered', warnings);
                modeler.get('canvas').zoom('fit-viewport');
            })
            .catch(function (err) {
                console.error('error rendering', err);
            });

        return modeler;
    },
    getXml: async function (modeler) {
        const { xml } = await modeler.saveXML({ format: true });
        return xml;
    },
    loadXml: async function (modeler, bpmnXml) {
        await modeler.importXML(bpmnXml);
        modeler.get('canvas').zoom('fit-viewport');
    }
};