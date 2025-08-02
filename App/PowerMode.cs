namespace PowerSaver.App;

public enum PowerMode
{
    Soft,   // Only switch power plan
    Medium, // Power plan + lower refresh rate
    Hard    // Everything above + CPU throttle
}