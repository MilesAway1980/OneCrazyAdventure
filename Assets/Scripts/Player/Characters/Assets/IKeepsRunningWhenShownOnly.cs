namespace Characters
{
    // Marks a script on a character that carries on working on a machine that has joined someone else's game, where the character is
    // only shown (see CharacterParking). It's for the parts of a character that only draw something: a flashlight's head and beam,
    // for example. Such a script has to leave the simulating to the host, and show what it's sent. Anything without this is switched
    // off there.
    public interface IKeepsRunningWhenShownOnly
    {
    }
}
