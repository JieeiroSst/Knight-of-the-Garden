// Vi tri nong trai (ham THUAN): luoi o cach nhau FARM_SPACING don vi the gioi (20 don vi = 1 m ->
// 100 000 = 5 km giua 2 tam nong trai). Thu tu cap: vong xoan oc tu trong ra ngoai, bo o (0,0).
const UNITS_PER_METER = 20;
const FARM_SPACING = 5000 * UNITS_PER_METER;

// Slot thu n (0-based) trong vong xoan: vong 1 co 8 o, vong 2 co 16 o...
function* spiral() {
  for (let r = 1; ; r++) {
    for (let x = -r; x <= r; x++) yield [x, -r];
    for (let z = -r + 1; z <= r; z++) yield [r, z];
    for (let x = r - 1; x >= -r; x--) yield [x, r];
    for (let z = r - 1; z > -r; z--) yield [-r, z];
  }
}

const slotToWorld = (gx, gz) => ({ x: gx * FARM_SPACING, z: gz * FARM_SPACING });

module.exports = { UNITS_PER_METER, FARM_SPACING, spiral, slotToWorld };
