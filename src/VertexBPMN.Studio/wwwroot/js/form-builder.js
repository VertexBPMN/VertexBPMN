
window.FormBuilderInterop = {
    createForm: function (containerId, formJson) {
        const schema = JSON.parse(formJson);

        const form = new FormEditor.FormEditor({
            container: document.getElementById(containerId),
            schema: schema
        });

        return form;
    },
    getJson: async function (form) {
        return JSON.stringify(form.getSchema(), null, 2);
    },
    loadJson: async function (form, formJson) {
        const schema = JSON.parse(formJson);
        form.importSchema(schema);
    }
};
