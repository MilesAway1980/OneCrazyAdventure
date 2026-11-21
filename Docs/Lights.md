# Lights

A light in the world is a **Light2D** run by a **LightSource**. (`LightSource` used to be called `LightTileSource`, from when lights lit
tiles. It's the same component, with the same settings: renaming it changed nothing saved in the scenes.)

## LightSource

It runs the Light2D on the same object, adding a glow that can pulse, and a **lit** and an **unlit** state. Put your own Light2D on the
object to choose its colour, falloff and so on. Only one LightSource belongs on an object.

- **Lit Look** and **Unlit Look** say how the light looks in each state: its **radius** and **intensity** (each can pulse between a low
  and a high value, as smoothly, steadily, in strobes or in a flicker), and whether its colour moves between a dim and a bright colour as it pulses.
  The unlit look is the **lowest** the light goes and the lit look is the **brightest**.
- **Fade Seconds** is how long it eases from one look to the other.
- **Time Until Go Out** puts it out by itself that long after it's lit.
- **Lit** is whether it starts lit. **Light**, **Snuff**, **SetLit** and **SetBoost** change it from code.
- A **Sconce** shows a lit or unlit sprite to match its light.

## Switching a light

A **Switch Light** (a switchable object, so a lever or a button works it) lights a LightSource, puts it out, or turns it up and down. Put it on the
light's object, or give it the light in **Light Source**.

| Setting | What it does |
|---|---|
| **Off Level** | How lit the light is while this is switched **off**: 0 is the light's **Unlit** look (its lowest, and how it starts the game), 1 is its **Lit** look (its brightest), and in between is a mix of the two. Default 0. |
| **On Level** | How lit it is while switched **on**. Default 1, the lit look. Set it to 0 for a switch that puts a light out. |
| **Fade Seconds** | How long it takes to change level. Under 0 (the default) uses the light's own Fade Seconds. 0 is at once. |

- To **fine tune the lowest and brightest**, set the light's own **Unlit Look** and **Lit Look**. To make a switch only turn a light down, or only up
  part of the way, set Off Level and On Level between 0 and 1.
- A button with **Allow Partial Presses** sets the level by how far it's pressed, anywhere between Off Level and On Level.
- **Stay Switched On** and **Switch Off Time** (on the Switch Light) work as they do on a door. The light's own **Time Until Go Out** still puts it
  out by itself.
- **A chain of lights:** put the sconces' Switch Lights in a lever's or a button's list, and set its **Delay Between Objects**. They light one
  after another, in the order of the list ([Switches.md](Switches.md)). **Switch Off Order** can put them out the other way round.
- In a network game the host does it. `LightSourceNetwork` sends both whether the light is lit and how lit it is, so every machine shows it. It's added
  to lights that aren't on characters by **Tools > Network > Make World Networked**.
