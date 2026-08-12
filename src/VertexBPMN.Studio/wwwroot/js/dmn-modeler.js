
window.DmnModelerInterop = {
    createModeler: function (containerId, dmnXml) {
        const modeler = new DmnJS({
            container: `#${containerId}`
        });

        modeler.importXML(dmnXml)
            .then(function (result) {
                const { warnings } = result;
                console.log('rendered', warnings);
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
    loadXml: async function (modeler, dmnXml) {
        await modeler.importXML(dmnXml);
    }
};
