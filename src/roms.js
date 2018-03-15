const fs = require('fs-extra');
const path = require('path');
const csvparse = require('csv-parse/lib/sync');

// delimiter
const defaultDelimiter = ';';

module.exports = {
    /**
     * Adds roms from a romset to a selection,
     * based on a CSV file
     * 
     * @param {string} file The path to the file
     * @param {string} romset The path to the romset folder
     * @param {string} selection The path to the selection folder
     */
    add: function add (file, romset, selection) {
        let fileCsv = csvparse(
            fs.readFileSync(file),
            {
                columns: true,
                auto_parse: false,
                auto_parse_date: false,
                delimiter: defaultDelimiter
            });

        for (let i = 0; i < fileCsv.length; i++) {
            let zip = fileCsv[i].name + '.zip';
            let sourceRom = path.join(romset, zip);
            let destRom = path.join(selection, zip);

            try {
                // test if destination file exists
                fs.accessSync(destRom, fs.constants.F_OK);
                console.log('Rom %s already exists', destRom);
            } catch (errDest) {
                // destination rom does not exist (accessSync has thrown)
                try {
                    // test if source rom exists
                    fs.accessSync(sourceRom, fs.constants.R_OK);
                    
                    // copy rom
                    fs.copySync(sourceRom, destRom);

                    console.log('%s copied', sourceRom);
                } catch (errSource) {
                    console.log('Unable to access %s - %o', sourceRom, errSource);
                }
            }
        }
    },

    /**
     * Removes roms from a selection,
     * based on a CSV file
     * 
     * @param {string} file The path to the file
     * @param {string} selection The path to the selection folder
     */
    remove: function remove (file, selection) {
        let fileCsv = csvparse(
            fs.readFileSync(file),
            {
                columns: true,
                auto_parse: false,
                auto_parse_date: false,
                delimiter: defaultDelimiter
            });

        for (let i = 0; i < fileCsv.length; i++) {
            let zip = fileCsv[i].name + '.zip';
            let rom = path.join(selection, zip);

            try {
                // test if rom exists
                fs.accessSync(rom, fs.constants.W_OK);
                
                // delete rom
                fs.unlinkSync(rom);

                console.log('%s deleted', rom);
            } catch (errRom) {
                console.log('Unable to delete %s - %o', rom, errRom);
            }
        }
    },


    /**
     * Removes roms not listed from a selection,
     * based on a CSV file
     * 
     * @param {string} file The path to the file
     * @param {string} selection The path to the selection folder
     */
    filter: function filter (file, selection) {
        let fileCsv = csvparse(
            fs.readFileSync(file),
            {
                columns: true,
                auto_parse: false,
                auto_parse_date: false,
                delimiter: defaultDelimiter
            });

        // list files in selection folder
        var files = fs.readdirSync(selection);
        for (let i = 0; i < files.length; i++) {
            let zip = files[i];

            // skip non-zip files
            if (!zip.endsWith('.zip')) { continue; }

            // file not found in csv -> remove it
            let csvItem = fileCsv.find((item) => item.name === zip.replace('.zip', ''));
            if (typeof secondaryItem === 'undefined') {
                console.log('remove %s', zip);
                fs.unlinkSync(path.join(selection, zip));
            }
        }
    }
};