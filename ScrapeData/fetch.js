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

    const findSection = (title) => {
        const res = $container.find('.card').filter((i, el) => $(el).find('> .card-body > .card-title').text().trim() == title)
        if (res.length > 1) throw new Error()
        return res
    }

    let name = cleanstr($container.find(`div.align-self-center > a[href=${path}].itemname`).text())
    if (hasTabs && expectVariant) {
        name += " (Special)"
    }

    let minWildLevel = null, maxWildLevel = null

    const $spawners = findSection('Spawner')
    if ($spawners.length) {
        const spawners = $spawners.find('tr td:nth-of-type(2)').toArray().map(el => cleanstr($(el).text()))
        const spawnerLevels = spawners.map(l => /(\d+)\s*.\s*(\d+)$/.exec(l)).filter(i => i)

        for (const [, smin, smax] of spawnerLevels) {
            const min = parseInt(smin)
            const max = parseInt(smax)
            if (minWildLevel === null || maxWildLevel === null) {
                minWildLevel = min
                maxWildLevel = max
            } else {
                minWildLevel = Math.min(minWildLevel, min)
                maxWildLevel = Math.max(maxWildLevel, max)
            }
        }
    }

    let exclusiveBreeding = null
    const $breeding = findSection('Breeding Farm')
    if ($breeding.length) {
        const parts = $breeding.find('a.itemname').toArray().map(el => /\w+$/.exec($(el).attr('data-hover'))[0])

        if (parts.length != 3 && parts.length) throw new Error()
        
        if (parts.length) {
            const genders = $breeding.find('img[src*=Gender]').toArray().map(el => /Gender_(\w+)\.webp/.exec($(el).attr('src'))).filter(i => i)

            const [p1, p2, child] = parts
            const [p1g, p2g] = genders.length == 2
                ? genders.map(([, g]) => g)
                : []

            exclusiveBreeding = {
                p1: { pal: p1, gender: p1g || null },
                p2: { pal: p2, gender: p2g || null },
                child
            }
        }
    }

    return {
        iconUrl: $container.find('.itemPopup a[data-hover] > img.rounded-circle').attr('src'),
        properties: extractProperties($container),
        minWildLevel,
        maxWildLevel,
        name,
        exclusiveBreeding
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

    // fs.writeFileSync('pals.json', JSON.stringify(pals, null, 4))

    const breedingInfo = await fetchBreedingRanks()
    // fs.writeFileSync('breedingInfo.json', JSON.stringify(breedingInfo, null, 4))
    const passives = await fetchPassives()
    // fs.writeFileSync('passive.json', JSON.stringify(passives, null, 4))

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

            MinWildLevel: parsed.minWildLevel,
            MaxWildLevel: parsed.maxWildLevel,

            ExclusiveBreeding: parsed.exclusiveBreeding ? {
                Parent1: {
                    CodeName: parsed.exclusiveBreeding.p1.pal,
                    RequiredGender: parsed.exclusiveBreeding.p1.gender,
                },
                Parent2: {
                    CodeName: parsed.exclusiveBreeding.p2.pal,
                    RequiredGender: parsed.exclusiveBreeding.p2.gender,
                },
                Child: parsed.exclusiveBreeding.child,
            } : null,
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

    // output raw data as CSV just as a reference for others using this data
    if (!fs.existsSync('out/csv')) fs.mkdirSync('out/csv')
    
    const palColumns = ['Name', 'CodeName']
    const ignoredPalColumns = ['GuaranteedTraits', 'ExclusiveBreeding']
    for (const k of Object.keys(resultPals[0]).filter(k => !ignoredPalColumns.includes(k))) {
        if (!palColumns.includes(k)) palColumns.push(k)
    }

    fs.writeFileSync('out/csv/pals.csv', [
        palColumns.join(','),
        ...resultPals.map(p => palColumns.map(c => p[c]).join(','))
    ].join('\n'))

    fs.writeFileSync('out/csv/unique_breeding.csv', [
        'Parent1,Parent2,Child',
        ...resultPals
            .map(p => p.ExclusiveBreeding)
            .filter(i => i)
            .map(({ Parent1: { CodeName: Parent1 }, Parent2: { CodeName: Parent2 }, Child }) => [ Parent1, Parent2, Child ].map(cn => resultPals.find(p => p.CodeName == cn).Name).join(','))
    ].join('\n'))

    fs.writeFileSync('out/csv/guaranteed_traits.csv', [
        'Pal,Trait1,Trait2,Trait3,Trait4',
        ...resultPals.filter(p => p.GuaranteedTraits.length).map(({ Name, GuaranteedTraits }) =>
            [ Name, ...GuaranteedTraits.map(t => passives.find(p => p.codeName == t).name), ...new Array(4 - GuaranteedTraits.length).map(v => '') ].join(',')
        )
    ].join('\n'))

    fs.writeFileSync('out/csv/traits.csv', [
        'Name,CodeName,Rank',
        ...passives.map(({ name, codeName, rank}) => [ name, codeName, rank ].join(','))
    ].join('\n'))
})()