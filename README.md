# SearchByStringExtensions

This library is intended to remove the pre-defined way of searches. The traditional way of searching required the user to receive pre-defined entity attributes (defined by developers) by which he could perform certain searches. This library has the task of enabling not only developers but also ordinary users to easily and quickly define and use their search queries.

# Who is this for?

This is primarily intended for developers to expose business users to complete freedom in choosing search attributes through their services, API, etc... As such, the library is intended to sit on top of the Entity Framework. All methods within the implementation itself are adapted to Entity Framework, so that it would later translate to the most optimal queries to the database itself. Of course, it can be used for other purposes...

# Installation
```
dotnet add package Search.By.String.Extensions --version 1.0.2
```
# Usage
```Where(string)``` is an extension method on ```IQueryable<T>``` (same as all other queryable Linq methods), and it takes one argument:
```c#
  Where(searchString)
```

# What is searchString and how to use it?

The **searchsString** is combination of  **attributes**, **logical operators**, **comparasion operators** and **brackers**.
To understand need for searchString, lets take a look on next example:

```c#
Genre action = new Genre { Id = 1, Name = "Action" };
Genre drama = new Genre { Id = 2, Name = "Drama" };
Genre comedy = new Genre { Id = 3, Name = "Comedy" };

Movie matrixMovie = new Movie 
{ 
    Id = 1, 
    Title = "Matrix",
    ReleaseYear = 1999, 
    Genres = new List<Genre> 
    { 
        action, 
        drama 
    } 
};

Movie forrestGumpMovie = new Movie 
{ 
    Id = 2, 
    Title = "Forrest Gump",
    ReleaseYear = 1994, 
    Genres = new List<Genre> 
    { 
        drama, 
        comedy 
    } 
};

List<Movie> movies = new List<Movie> { matrixMovie, forrestGumpMovie };

public class Genre
{
    public int Id { get; set; }
    public string Name { get; set; }
}

public class Movie
{
    public int Id { get; set; }
    public string Title { get; set; }
    public int ReleaseYear { get; set; }
    public List<Genre> Genres { get; set; }
}
```

To search movies who has release year after 1994, the tradicional ```C#``` code would look like this:
```c#
var result = movies.Where(exp => exp.ReleaseYear > 1994)
                    .ToList();
```
To search movies who has release year after 1994, using ```searchString``` parameter, the code would like this:
```c#
var result = movies.AsQueryable()
                   .Where("ReleaseYear>1994")
                   .ToList();
```
To search movies who has release year after 1994 and one of Genres is Action using ```searchString``` parameter, the code with nested attributes would like this:
```c#
var result = movies.AsQueryable()
                   .Where("ReleaseYear>1994andGenres.Name=Action")
                   .ToList();
```

# What is it ready for?

This is example of complex usage of ```searchString```
```c#
var result = movies.AsQueryable()
                   .Where("(ReleaseYear>1994orGenres.Name=Action)and(Id>3or(Title=MatrixandReleaseYear=1999))")
                   .ToList();
```
# What comparasion operators can I use?

|     Name      | Description     |
| ------------- | ------------- |
|       !=      | Not equals    |
|       >=      | Greater or equal than  |
|       <=      | Less or equal than  |
|       <     | Less  than  |
|       >    | Greater  than  |
|       =    | Equal  |
|       contains   | Consists of the given string (Like %givenString%)  |
|       startswith   | Starts with the given string (Like 'givenString%'|
|       endsswith   | Ends with the given string  (Like '%givenString')|
|       empty   | Is null (IS NULL) |

# What logical operators can I use?

|     Name      | Description     |
| ------------- | ------------- |
|       and     | Logical AND   |
|       or     | Logical OR   |

# On top Entity Framework

How the complex query would be translated into SQL:
```sql
SELECT m.Id, m.Title, m.ReleaseYear
FROM movies as m
LEFT OUTER JOIN genres as gen
ON gen.MovieId = m.Id
WHERE (m.ReleaseYear > 1994 AND EXISTS (SELECT TOP 1 Id FROM genres WHERE Id = gen.Id and Name = 'Action'))
AND (m.Id > 3 OR (m.Title = 'Matrix' AND m.ReleaseYear = 1999))
```




