// ty chatgpt for template

const fs = require('fs');
const axios = require('axios');
const cheerio = require('cheerio');
const path = require('path');

// Function to parse HTML and extract image URLs
function parseHTML(html) {
    const $ = cheerio.load(html);
    const imageUrls = [];

    // Change the selector to match the HTML structure of your page
    $('.pals-list .pal').each((index, entry) => {
        const $entry = $(entry)

        const $name = $entry.find('.container > .name')
        const match = /(.+) \#\d+/.exec($name.text())

        if (!match) return;

        const name = match[1]
        const imageUrl = $entry
            .find('.container > .image img')
            .attr('src')
            .replace('/palicon/', '/full_palicon/')

        imageUrls.push({ name, imageUrl })
    });

    return imageUrls;
}

// Function to download images
async function downloadImages(imageUrls) {
    if (!fs.existsSync('out'))
        fs.mkdirSync('out')

    for (const { name, imageUrl } of imageUrls) {
        const fullUrl = 'https://palworld.gg' + imageUrl
        try {
            const response = await axios.get(fullUrl, { responseType: 'stream' });
            const imageName = name + '.png'
            const imagePath = path.join(__dirname, 'out/' + imageName);

            if (fs.existsSync(imagePath)) fs.rmSync(imagePath)

            response.data.pipe(fs.createWriteStream(imagePath));
            console.log(`Downloaded: ${imageName}`);
        } catch (error) {
            console.error(`Error downloading ${fullUrl}: ${error.message}`);
        }
    }
}

// Read HTML file from disk
fs.readFile('Palworld Pals - Full Paldeck.html', 'utf8', (err, html) => {
    if (err) {
        console.error(`Error reading HTML file: ${err.message}`);
        return;
    }

    // Parse HTML and extract image URLs
    const imageUrls = parseHTML(html);
    console.log(imageUrls)

    // Download images
    downloadImages(imageUrls);
});
