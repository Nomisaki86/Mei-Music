Folder Responsibilities:

Models
    - The data types - what things are
    - plain objects that hold info, objects

ViewModels
    - The brain - what the app does with the data
    - middle man between teh UI and the rest of the app

Views
    - what the user actually sees - the UI

Services
    - specialist workers - how complicated thigns get done
    - audio services
    - File Services
    - play list Services

Converters
    - Translate between data and UI

Resources
    - Design assets

Dependency Injection (DI) 
    - Sharing Data/Singletons, if MainWIndow crates a new AudioPlayerService() and another window does the same, we now have 2 running at the same time. 
    - Unit Testing, can swap out the real FileService with a "Fake" one just for the test.
    - Cleaner code, classes stop having to worry about how to build their dependencies and just focus on doing their jobs.
    - This works just like Awake() or Start() in Unity, 1 place for setting up.

# Liked -> All takes long time
`Problem`: Moving from Liked Songs view to All Songs view is slow because WPF is trying to creating all the songs before displaying them.  
`Solition`: Introduced ListView, which WBP is smart enought to use it for `UI Virtualization` which calculate your screen space and only render the items within that space. But this introduces a new problem. ListView is designed to work with a single stream of data (1 type of object). If I want the `header(Type A)` and the `songs(Type B)` to be both scrollable I need to use a `CompositeCollection`. 
    