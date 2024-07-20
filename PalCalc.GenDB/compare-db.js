const fs = require('fs')

const oldDbPath = 'C:/Users/algor/Downloads/db.json'
const newDbPath = '../PalCalc.Model/db.json'

const oldDb = JSON.parse(fs.readFileSync(oldDbPath).toString())
const newDb = JSON.parse(fs.readFileSync(newDbPath).toString())

/* COMPARE PALS */

const toObj = (arr, key) => {
    const res = {}
    arr.forEach(v => res[v[key]] = v)
    return res
}

const oldPalsByInternalName = toObj(oldDb.Pals, 'InternalName')
const newPalsByInternalName = toObj(newDb.Pals, 'InternalName')

const addedPals = Object.keys(newPalsByInternalName).filter(p => !oldPalsByInternalName[p])
const removedPals = Object.keys(oldPalsByInternalName).filter(p => !newPalsByInternalName[p])
const keptPals = Object.keys(oldPalsByInternalName).filter(p => newPalsByInternalName[p])

console.log('added pals', addedPals)
console.log('removed pals', removedPals)
console.log('kept pals', keptPals.length)

console.log('checking for diffs')
for (const existing of keptPals) {
    const fromOld = oldPalsByInternalName[existing]
    const fromNew = newPalsByInternalName[existing]

    const toCheck = ['Id', 'Name', 'InternalIndex', 'BreedingPower', 'GuaranteedPassiveSkillsInternalIds']
    const changed = toCheck.filter(k => JSON.stringify(fromOld[k]) != JSON.stringify(fromNew[k]))

    if (changed.length) {
        console.log(`Changes in ${fromOld.Name}:`)
        for (const k of changed) {
            console.log('- ' + k)
            console.log('-   old: ' + JSON.stringify(fromOld[k]))
            console.log('-   new: ' + JSON.stringify(fromNew[k]))
        }
    }
}

/* COMPARE GENDER PROBABILITY */
console.log('checking gender probabilities')
for (const existing of keptPals) {
    const name = newPalsByInternalName[existing].Name

    const oldProbability = oldDb.BreedingGenderProbability[name]
    const newProbability = newDb.BreedingGenderProbability[name]

    if (JSON.stringify(oldProbability) != JSON.stringify(newProbability)) {
        console.log(`- ${name} changed from`, oldProbability, 'to', newProbability)
    }
}

/* COMPARE PASSIVES */
const oldPassivesByInternalName = toObj(oldDb.Traits || oldDb.PassiveSkills, 'InternalName')
const newPassivesByInternalName = toObj(newDb.Traits || newDb.PassiveSkills, 'InternalName')

const addedPassives = Object.keys(newPassivesByInternalName).filter(t => !oldPassivesByInternalName[t])
const removedPassives = Object.keys(oldPassivesByInternalName).filter(t => !newPassivesByInternalName[t])
const keptPassives = Object.keys(oldPassivesByInternalName).filter(t => newPassivesByInternalName[t])

console.log('added passives', addedPassives)
console.log('removed passives', removedPassives)

console.log('checking for diffs')
for (const existing of keptPassives) {
    const fromOld = oldPassivesByInternalName[existing]
    const fromNew = newPassivesByInternalName[existing]

    const toCheck = ['Name', 'Rank']
    const changed = toCheck.filter(k => fromOld[k] != fromNew[k])

    if (changed.length) {
        console.log(`Changes in ${fromOld.Name}`)
        for (const k of changed) {
            console.log('- ' + k)
            console.log('-   old: ' + fromOld[k])
            console.log('-   new: ' + fromNew[k])
        }
    }
}

/* SPOT-CHECK SOME BREEDING RESULTS */

const childOf = (id1, id2) => newDb.Breeding.find(({ Parent1ID, Parent2ID }) => (
    (JSON.stringify(Parent1ID) == JSON.stringify(id1) && JSON.stringify(Parent2ID) == JSON.stringify(id2)) ||
    (JSON.stringify(Parent2ID) == JSON.stringify(id1) && JSON.stringify(Parent1ID) == JSON.stringify(id2))
))?.ChildID

console.log(childOf(
    { PalDexNo: 100, IsVariant: false },
    { PalDexNo: 13, IsVariant: false }
))

console.log(childOf(
    { PalDexNo: 101, IsVariant: true },
    { PalDexNo: 16, IsVariant: false }
))

console.log(childOf(
    { PalDexNo: 37, IsVariant: false },
    { PalDexNo: 36, IsVariant: false }
))