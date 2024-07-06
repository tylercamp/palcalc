// ty chatgpt for template

const fs = require('fs');
const axios = require('axios').default;
const cheerio = require('cheerio');
const sharp = require('sharp')

/*
1. use paldb.cc to fetch the list of pals
2. use paldb.cc to fetch icons + details for each pal
3. use paldb.cc to fetch the list of traits and associated pals
4. scrape Palworld-Pal-Editor source to get the internal codenames for traits
*/

if (!fs.existsSync('out/raw-icons')) fs.mkdirSync('out/raw-icons', { recursive: true })
if (!fs.existsSync('out/icons')) fs.mkdirSync('out/icons', { recursive: true })
if (!fs.existsSync('cache')) fs.mkdirSync('cache')

const delay = ms => new Promise(resolve => setTimeout(resolve, ms || (1000 + Math.random() * 5000)))
const cleanstr = s => s.trim().replace(/\s+/g, ' ')

const PALDB_CC_BASE = "https://paldb.cc/en/"
const PALDB_URL = path => PALDB_CC_BASE + path;
const PALDB_CACHED_GET = async (path) => {
    const cachedPath = `cache/PALDB_${path}`
    if (fs.existsSync(cachedPath)) {
        return fs.readFileSync(cachedPath).toString()
    } else {
        console.log('getting', path)
        await delay()
        const res = (await axios.get(PALDB_URL(path)).catch(console.log)).data
        fs.writeFileSync(cachedPath, res)
        return res
    }
}

// returns a list of subpaths to individual pages of pals
async function collectPalList() {
    const index = await PALDB_CACHED_GET('Pals')
    const $ = cheerio.load(index)

    const $entries = $('#Pal div.col[data-filters]').filter((i, el) => !$(el).find('img[src*=unknown]').length)

    console.log(`found ${$entries.length} pal entries`)
    const pals = $entries.toArray()
        .map((el) => ({
            palPath: $(el).find('a[href]').attr('href'),
            paldexNo: cleanstr($(el).find('.small').text()),
            name: cleanstr($(el).find('a.itemname').text()),
        }))
        .filter(({ paldexNo }) => /#\d+/.exec(paldexNo))
    
    console.log(`reduced to ${pals.length} pals`)

    return pals
}

async function parsePalUrl(path, expectVariant) {
    const page = await PALDB_CACHED_GET(path)
    const $ = cheerio.load(page)

    function extractProperties($container) {
        const tableRows = $container.find('.card > .card-body .d-flex.justify-content-between.p-2')

        const properties = {}
        tableRows.each((i, el) => {
            const key = cleanstr($(el).find('> *:first-child').text())
            const value = cleanstr($(el).find('> *:last-child').text())
            properties[key] = value
        })

        return properties
    }

    // really just a check for "Gumoss (Special)" (#13B)
    let $container;
    const hasTabs = !!$('#Pals').length
    if (hasTabs) {
        $container = $('*[id*=Pals]').filter((i, el) => {
            const props = extractProperties($(el))
            const matchesVariant = (props["ZukanIndexSuffix"] == "B") == expectVariant
            return matchesVariant && props["CombiRank"] != 9999
        })
    } else {
        $container = $('.page-content')
    }

    // console.log($container.length)

    if ($container.length != 1) {
        throw new Error()
    }
    $container = $($container[0])

    let name = cleanstr($container.find(`div.align-self-center > a[href=${path}].itemname`).text())
    if (hasTabs && expectVariant) {
        name += " (Special)"
    }

    return {
        iconUrl: $container.find('.itemPopup a[data-hover] > img.rounded-circle').attr('src'),
        properties: extractProperties($container),
        name,
    }
}

async function fetchBreedingRanks() {
    const $ = cheerio.load(await PALDB_CACHED_GET('Breeding_Farm'))

    return $('#BreedCombi tr').toArray().map(el => ({
        name: cleanstr($(el).find('td:nth-of-type(1)').text()),
        combiRank: cleanstr($(el).find('td:nth-of-type(2)').text()),
        indexOrder: cleanstr($(el).find('td:nth-of-type(3)').text()),
    }))
}

async function fetchPassives() {
    const $ = cheerio.load(await PALDB_CACHED_GET('Passive_Skills'))

    return $('#PalPassiveSkills .col').toArray().map(el => ({
        name: cleanstr($(el).find('div.passive_banner_inner_rank').text()),
        codeName: /PassiveSkills\/(.+)$/.exec(
            $(el).find('div.passive_banner_inner_rank div[data-hover]').attr('data-hover')
        )[1],
        guaranteedForPalNames: $(el).find('a[data-hover*=Pals]').toArray().map(a => $(a).attr('href')),
        rank: /passive_banner_rank([\-\d]+)/.exec(
            $(el).find('div[class*=passive_banner_rank]').attr('class')
        )[1],
    }))
}

;(async function() {
    const pals = await collectPalList()

    fs.writeFileSync('pals.json', JSON.stringify(pals, null, 4))

    const breedingInfo = await fetchBreedingRanks()
    fs.writeFileSync('breedingInfo.json', JSON.stringify(breedingInfo, null, 4))
    const passives = await fetchPassives()
    fs.writeFileSync('passive.json', JSON.stringify(passives, null, 4))

    const resultPals = []

    for (const { palPath, paldexNo, name } of pals) {
        console.log('parsing', palPath, paldexNo)
        const parsed = await parsePalUrl(palPath, paldexNo.endsWith("B"))

        const breedingEntry = breedingInfo.find(i => i.name == name)
        if (!breedingEntry) {
            console.log('no matching breeding entry for ' + name)
        }
        resultPals.push({
            Name: parsed.name,
            CodeName: parsed.properties["Code"],
            PalDexNo: parseInt(parsed.properties["ZukanIndex"]),
            IsVariant: parsed.properties["ZukanIndexSuffix"] == "B",
            BreedPower: parseInt(parsed.properties["CombiRank"]),
            MaleProbability: parseInt(parsed.properties["MaleProbability"]),
            GuaranteedTraits: passives.filter(({ guaranteedForPalNames }) => guaranteedForPalNames.includes(palPath)).map(p => p.codeName),
            Price: parseInt(parsed.properties["Gold Coin"]),

            IndexOrder: breedingEntry ? parseInt(breedingEntry.indexOrder) : -1,
        })

        // fs.writeFileSync('parsed.json', JSON.stringify(parsed, null, 4))
        // return

        const rawIconPath = 'out/raw-icons/' + name + '.webp'
        if (!fs.existsSync(rawIconPath)) {
            console.log(`storing ${parsed.iconUrl} to ${rawIconPath}`)
            try {
                await delay();
                const iconResponse = await axios.get(parsed.iconUrl, { responseType: 'stream' }).catch(console.log)
                iconResponse.data.pipe(fs.createWriteStream(rawIconPath))
            } catch (e) {
                console.log('icon fetch failed!')
            }
        }

        const convertedIconPath = 'out/icons/' + name + '.png'
        if (!fs.existsSync(convertedIconPath) && fs.existsSync(rawIconPath)) {
            fs.writeFileSync(
                convertedIconPath,
                await sharp(rawIconPath)
                    .resize({ width: 100, height: 100 })
                    .toFormat('png')
                    .toBuffer()
            )
        }
    }

    fs.writeFileSync('out/scraped-pals.json', JSON.stringify(resultPals, null, 4))

    fs.writeFileSync('out/scraped-traits.json', JSON.stringify(
        passives.map(({ name, codeName, rank }) => ({
            Name: name, CodeName: codeName, Rank: parseInt(rank), IsPassive: true
        })),
        null,
        4
    ))
})()