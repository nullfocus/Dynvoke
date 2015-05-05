# Dynvoke

## Features
- Super simple standalone Mono/.NET based JSON HTTP server library!

- Just add a single attribute to a static class and you’re off to the races! 

- Auto-generates Javascript XMLHttpRequest functions, as well as an Angular $http ready service based on the methods you include!

- Filtering of method parameters so you don't leak your internal autentication or user classes externally!

- Also auto-generates Javascript objects based on the object types your methods require! 

- Use the included default HttpListener implementation, or plug in your own easily!

- Alias your controller and action names using attribute options, and set webservice inclusion to implicit or explicit!

## Example

Here’s an  example of setting up Dynvoke with the default HttpListener implementation:

```
using (var server = new HttpDynvokeServer("localhost", 6543, "api")) //name or IP, port, js namespace
{
    server.Start(); //default impl runs on it’s own listener thread
    
    while (true)
        Thread.Sleep (1000); //do some other work, wait for shutdown evt, etc...
}
```

Here’s an example class which is now accessible as a JSON webservice:
```
[DynvokeClass]
public static class MyEndpoint
{
    public static string hello(string name)
    {
        return string.Format("hello there {0}!", name);
    }
}
```
