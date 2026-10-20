import argparse
import random
import re
import sys
from collections import deque

SOLID = set("# AL")
CLOSED_GATE = set("PH")
OBSTACLE = set("*~m<uUkKjJ")
FLYABLE_EXTRA = set("~uUkKjJm/")
DIRECTIONS = [(0, -1), (1, -1), (1, 0), (1, 1), (0, 1), (-1, 1), (-1, 0), (-1, -1)]
CELL = re.compile(r"\((\d+),(\d+)\)")


class Monster:
    def __init__(self, ident, kind, pos, tracking, stun, mutating):
        self.ident = ident
        self.kind = kind
        self.pos = pos
        self.tracking = tracking
        self.stun = stun
        self.mutating = mutating

    @property
    def is_bat(self):
        return self.kind == "chauve-souris"

    @property
    def active(self):
        return self.stun == 0 and not self.mutating


class State:
    def __init__(self, text):
        self.rows = []
        self.monsters = []
        self.magus = None
        self.magic = 0
        self.range = 5
        self.fire = set()
        self.water = set()
        self.legal = {}
        self.exits = set()
        self._parse(text)
        self.height = len(self.rows)
        self.width = max((len(row) for row in self.rows), default=0)

    def _parse(self, text):
        lines = text.splitlines()
        in_map = False
        in_actions = False
        for index, line in enumerate(lines):
            magus = re.match(r"Magus en \((\d+),(\d+)\), magie (\d+)/(\d+) \(sort : coût (\d+), portée (\d+)\)", line)
            if magus:
                self.magus = (int(magus.group(1)), int(magus.group(2)))
                self.magic = int(magus.group(3))
                self.range = int(magus.group(6))
                continue
            if line.startswith("Carte"):
                in_map = True
                continue
            if in_map:
                row = re.match(r"^\s*(\d+) (.*)$", line)
                if row and not set(line.strip()) <= set("0123456789 "):
                    self.rows.append(row.group(2).rstrip("\n"))
                    continue
                if line.strip() == "" and self.rows:
                    in_map = False
                continue
            creature = re.match(r"- n°(\d+) (blob vert|blob rouge|chauve-souris) en \((\d+),(\d+)\)(.*)$", line)
            if creature:
                rest = creature.group(5)
                stun = re.search(r"étourdi (\d+)", rest)
                self.monsters.append(Monster(
                    int(creature.group(1)),
                    creature.group(2),
                    (int(creature.group(3)), int(creature.group(4))),
                    "vous traque" in rest,
                    int(stun.group(1)) if stun else 0,
                    "en mutation" in rest))
                continue
            if line.startswith("Cases que les blobs verts évitent"):
                self.fire = {(int(x), int(y)) for x, y in CELL.findall(line)}
                continue
            if line.startswith("Cases que les blobs rouges évitent"):
                self.water = {(int(x), int(y)) for x, y in CELL.findall(line)}
                continue
            if line.startswith("Sorties"):
                self.exits = {(int(x), int(y)) for x, y in CELL.findall(line)}
                continue
            if line.startswith("Actions possibles"):
                in_actions = True
                continue
            if in_actions:
                action = re.match(r"- (\w+)(?: : (.*))?$", line)
                if action:
                    self.legal[action.group(1)] = [(int(x), int(y)) for x, y in CELL.findall(action.group(2) or "")]
                    continue
                if line.strip() == "" or not line.startswith("-"):
                    in_actions = False
        if self.magus is None or not self.rows:
            raise ValueError("État illisible : il faut la sortie complète de l'outil etat ou jouer pendant une partie.")

    def at(self, pos):
        x, y = pos
        if y < 0 or y >= len(self.rows) or x < 0 or x >= len(self.rows[y]):
            return " "
        return self.rows[y][x]

    def terrain(self, pos):
        symbol = self.at(pos)
        if symbol in "@brc":
            return "."
        return symbol

    def blocks_sight(self, pos):
        symbol = self.terrain(pos)
        return symbol in SOLID or symbol in CLOSED_GATE

    def walkable(self, pos):
        symbol = self.terrain(pos)
        return not (symbol in SOLID or symbol in CLOSED_GATE or symbol in OBSTACLE)

    def flyable(self, pos):
        symbol = self.terrain(pos)
        return symbol in ".>a!" or symbol in FLYABLE_EXTRA

    def corner_free(self, a, b):
        dx, dy = b[0] - a[0], b[1] - a[1]
        if dx == 0 or dy == 0:
            return True
        return not self.blocks_sight((b[0], a[1])) and not self.blocks_sight((a[0], b[1]))

    def line_of_sight(self, start, goal):
        nx, ny = abs(goal[0] - start[0]), abs(goal[1] - start[1])
        sx = (goal[0] > start[0]) - (goal[0] < start[0])
        sy = (goal[1] > start[1]) - (goal[1] < start[1])
        x, y = start
        ix = iy = 0
        while ix < nx or iy < ny:
            decision = (1 + 2 * ix) * ny - (1 + 2 * iy) * nx
            if decision == 0:
                if self.blocks_sight((x + sx, y)) and self.blocks_sight((x, y + sy)):
                    return False
                x += sx
                y += sy
                ix += 1
                iy += 1
            elif decision < 0:
                x += sx
                ix += 1
            else:
                y += sy
                iy += 1
            if (x, y) != goal and self.blocks_sight((x, y)):
                return False
        return True

    def feared(self, monster, pos):
        if monster.kind == "blob vert":
            return pos in self.fire
        if monster.kind == "blob rouge":
            return pos in self.water
        return False

    def steps(self, monster, goal, others):
        start = monster.pos
        if start == goal:
            return 0
        seen = {start: 0}
        queue = deque([start])
        while queue:
            current = queue.popleft()
            for dx, dy in DIRECTIONS:
                nxt = (current[0] + dx, current[1] + dy)
                if nxt in seen or not self.corner_free(current, nxt):
                    continue
                if nxt == goal:
                    return seen[current] + 1
                passable = self.flyable(nxt) if monster.is_bat else self.walkable(nxt)
                if not passable or nxt in others or self.feared(monster, nxt):
                    continue
                seen[nxt] = seen[current] + 1
                queue.append(nxt)
        return None

    def magus_steps_to_exit(self, start):
        if not self.exits:
            return None
        seen = {start: 0}
        queue = deque([start])
        while queue:
            current = queue.popleft()
            if current in self.exits:
                return seen[current]
            for dx, dy in DIRECTIONS:
                nxt = (current[0] + dx, current[1] + dy)
                if nxt in seen:
                    continue
                symbol = self.terrain(nxt)
                cost = 2 if symbol == "P" else 1
                if symbol == "P":
                    pass
                elif not self.walkable(nxt) or not self.corner_free(current, nxt):
                    continue
                seen[nxt] = seen[current] + cost
                queue.append(nxt)
        return None


class Perception:
    def __init__(self, level, rng):
        self.level = level
        self.rng = rng
        self.spread = 0.12 * (5 - level)
        self.biases = {}

    def estimate(self, key, value):
        if value is None:
            return None, "inaccessible"
        if self.level >= 5 or value <= 1:
            return value, str(value)
        if key not in self.biases:
            self.biases[key] = self.rng.uniform(-self.spread, self.spread)
        guess = max(2, round(value * (1 + self.biases[key])))
        margin = max(1, round(guess * self.spread))
        return guess, f"≈{guess} (±{margin})"

    def sight(self, value, distance):
        if self.level >= 5 or distance <= 2:
            return value
        if self.rng.random() < 0.06 * (5 - self.level):
            return not value
        return value


def chebyshev(a, b):
    return max(abs(a[0] - b[0]), abs(a[1] - b[1]))


def fmt(pos):
    return f"({pos[0]},{pos[1]})"


def candidates(state):
    result = [("attendre", state.magus)]
    for name in ("deplacer", "spacio_magus"):
        for pos in state.legal.get(name, []):
            result.append((name, pos))
    return result


def evaluate(state, perception, action, cell):
    blocking = {monster.pos for monster in state.monsters if not monster.active}
    notes = []
    danger = False
    hunters = []
    for monster in state.monsters:
        if not monster.active:
            if monster.stun:
                notes.append(f"n°{monster.ident} étourdi encore {monster.stun}")
            continue
        distance = chebyshev(monster.pos, cell)
        seen = perception.sight(state.line_of_sight(monster.pos, cell), distance)
        alert = monster.tracking or seen
        adjacent = distance == 1 and state.corner_free(monster.pos, cell)
        if alert and adjacent:
            if monster.is_bat:
                notes.append(f"MORSURE par n°{monster.ident} (−1 gemme)")
            elif state.feared(monster, cell):
                notes.append(f"n°{monster.ident} voisin mais a peur ici (protection temporaire)")
            else:
                notes.append(f"CAPTURE par n°{monster.ident}")
                danger = True
            continue
        if not alert:
            continue
        steps = state.steps(monster, cell, blocking)
        guess, text = perception.estimate(("monstre", monster.ident), steps)
        label = "traque" if monster.tracking else "vous verrait"
        where = f"à {text} pas" if guess is not None else "sans chemin jusqu'à cette case"
        notes.append(f"n°{monster.ident} {monster.kind} ({label}) {where}")
        if guess is not None:
            hunters.append(guess)
    exit_guess, exit_text = perception.estimate(("sortie",), state.magus_steps_to_exit(cell))
    margin = min(hunters) - 1 if hunters else None
    neighbours = {(cell[0] + dx, cell[1] + dy) for dx, dy in DIRECTIONS}
    containers = sorted(pos for pos in neighbours if state.terrain(pos) in "uUkKjJ")
    if containers:
        notes.append("contenant à briser à côté : " + " ".join(fmt(pos) for pos in containers))
    if state.terrain(cell) == "!":
        notes.append("fiole sur la case : magie remplie si elle n'est pas pleine")
    return {
        "action": action,
        "cell": cell,
        "danger": danger,
        "margin": margin,
        "notes": notes,
        "exit": exit_text if exit_guess is not None else "?",
        "fire": cell in state.fire,
        "water": cell in state.water,
    }


def main():
    parser = argparse.ArgumentParser(description="Analyse d'un état de Mob le Magus pour un joueur de niveau 1 à 5.")
    parser.add_argument("--niveau", type=int, default=5, choices=range(1, 6))
    parser.add_argument("--graine", type=int, default=None)
    parser.add_argument("fichier", nargs="?", help="Sortie de l'outil etat ou jouer (sinon entrée standard).")
    args = parser.parse_args()
    text = open(args.fichier, encoding="utf-8").read() if args.fichier else sys.stdin.read()
    try:
        state = State(text)
    except ValueError as error:
        print(error, file=sys.stderr)
        return 2
    perception = Perception(args.niveau, random.Random(args.graine))
    results = [evaluate(state, perception, action, cell) for action, cell in candidates(state)]
    results.sort(key=lambda r: (r["danger"], -(r["margin"] if r["margin"] is not None else 99)))
    precision = "exacte" if args.niveau >= 5 else f"approximative (niveau {args.niveau})"
    print(f"Analyse {precision} — magus {fmt(state.magus)}, magie {state.magic}")
    for result in results:
        tags = []
        if result["danger"]:
            tags.append("MORTEL")
        if result["fire"]:
            tags.append("zone feu")
        if result["water"]:
            tags.append("zone eau")
        margin = "aucun poursuivant" if result["margin"] is None else f"marge {result['margin']}"
        head = f"{result['action']:<12} {fmt(result['cell']):<8} sortie {result['exit']:<16} {margin}"
        if tags:
            head += " [" + ", ".join(tags) + "]"
        print(head)
        for note in result["notes"]:
            print(f"    - {note}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
