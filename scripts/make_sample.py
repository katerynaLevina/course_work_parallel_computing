"""Generate the sample dataset: data/sample/doc_000.txt .. doc_299.txt (stdlib only)."""
import random
from pathlib import Path

SEED = 42
DOC_COUNT = 300
MIN_SENTENCES = 3
MAX_SENTENCES = 8
OUT_DIR = Path(__file__).resolve().parent.parent / "data" / "sample"

TOPICS = {
    "ocean": ["ocean", "wave", "tide", "coral", "reef", "whale", "salt", "current", "shore", "depth"],
    "volcano": ["volcano", "lava", "magma", "crater", "eruption", "ash", "basalt", "vent", "caldera", "tremor"],
    "guitar": ["guitar", "string", "chord", "fret", "melody", "amplifier", "strum", "pick", "riff", "acoustic"],
    "football": ["football", "goal", "striker", "referee", "stadium", "penalty", "midfield", "league", "dribble", "tackle"],
    "chemistry": ["chemistry", "molecule", "reaction", "catalyst", "acid", "solvent", "compound", "electron", "bond", "lab"],
    "astronomy": ["astronomy", "planet", "galaxy", "telescope", "orbit", "comet", "nebula", "star", "eclipse", "cosmos"],
    "cooking": ["cooking", "recipe", "oven", "spice", "simmer", "dough", "sauce", "kitchen", "flavor", "chef"],
    "forest": ["forest", "pine", "moss", "canopy", "fern", "timber", "trail", "owl", "undergrowth", "grove"],
    "medicine": ["medicine", "doctor", "vaccine", "symptom", "therapy", "clinic", "diagnosis", "antibody", "patient", "surgery"],
    "computing": ["computing", "processor", "memory", "compiler", "thread", "network", "algorithm", "kernel", "cache", "server"],
    "history": ["history", "empire", "dynasty", "archive", "treaty", "monument", "ruins", "chronicle", "kingdom", "artifact"],
    "weather": ["weather", "storm", "cloud", "humidity", "thunder", "forecast", "breeze", "frost", "monsoon", "drizzle"],
    "painting": ["painting", "canvas", "brush", "pigment", "portrait", "easel", "palette", "gallery", "landscape", "fresco"],
    "railway": ["railway", "locomotive", "station", "track", "carriage", "signal", "timetable", "platform", "freight", "tunnel"],
    "desert": ["desert", "dune", "oasis", "camel", "mirage", "cactus", "sand", "caravan", "drought", "canyon"],
    "aviation": ["aviation", "runway", "pilot", "wing", "turbine", "cockpit", "altitude", "airport", "glider", "propeller"],
    "gardening": ["gardening", "seed", "soil", "blossom", "compost", "greenhouse", "pruning", "harvest", "orchard", "mulch"],
    "chess": ["chess", "pawn", "bishop", "gambit", "checkmate", "opening", "endgame", "knight", "rook", "tournament"],
    "architecture": ["architecture", "arch", "facade", "column", "blueprint", "foundation", "dome", "bridge", "tower", "cathedral"],
    "photography": ["photography", "lens", "shutter", "aperture", "exposure", "tripod", "negative", "flash", "focus", "darkroom"],
}

TEMPLATES = [
    "The {a} and the {b} are closely tied to {c}.",
    "Many people study {a} because {b} shapes how {c} works.",
    "A good {a} often depends on {b}, {c}, and patience.",
    "Experts say {a} changes when {b} meets {c}.",
    "Every {a} has its own {b}, but {c} is rarely ignored.",
    "Few things matter more than {a} when {b} and {c} collide.",
]


def make_doc(rng: random.Random) -> str:
    topic = rng.choice(list(TOPICS))
    words = TOPICS[topic]
    title = f"{topic.capitalize()} notes on {rng.choice(words)}"
    sentences = []
    for _ in range(rng.randint(MIN_SENTENCES, MAX_SENTENCES)):
        a, b, c = rng.sample(words, 3)
        sentences.append(rng.choice(TEMPLATES).format(a=a, b=b, c=c))
    return title + "\n" + " ".join(sentences) + "\n"


def main() -> None:
    rng = random.Random(SEED)
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    for i in range(DOC_COUNT):
        (OUT_DIR / f"doc_{i:03d}.txt").write_text(make_doc(rng), encoding="utf-8")


if __name__ == "__main__":
    main()
