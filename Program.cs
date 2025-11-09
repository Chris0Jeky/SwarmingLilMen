Console.WriteLine("Hello, Lil Men!");

string name = "Chris";
int count = 3;
string? maybe = null;
Console.WriteLine($"{name} x{count}");
Console.WriteLine(maybe is null ? "is null" : maybe.Length.ToString());

// Console.WriteLine(); 
// Console.Out.Flush();
// string? input = Console.ReadLine();
// Console.WriteLine($"You typed: {input}");

static int Add(int a, int b) => a + b;
static double Dist2(float x1, float y1, float x2, float y2) 
    => (x1 - x2)*(x1 - x2) + (y1 - y2)*(y1 - y2);

Console.WriteLine(Add(2, 5));
Console.WriteLine(Dist2(0, 0, 3, 4));

static string SpeedBand(float v) => v switch
{
    < 0.2f => "slow",
    < 1.0f => "average",
    < 2.0f => "fast",
    _ => "ultra fast"
}; 

Console.WriteLine(SpeedBand(0.05f));