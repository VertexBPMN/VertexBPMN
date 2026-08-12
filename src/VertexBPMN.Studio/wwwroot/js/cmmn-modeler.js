
window.CmmnModelerInterop = {
    createModeler: function (containerId, cmmnXml) {
        const modeler = new CmmnJS({
            container: `#${containerId}`
        });

        modeler.importXML(cmmnXml)
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
    loadXml: async function (modeler, cmmnXml) {
        await modeler.importXML(cmmnXml);
    }
};
