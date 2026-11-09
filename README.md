Part 3: Automation of the Web Application

What it’s about: Part 3 is the final part of the PoE. You build the web front end for RaceDay as an ASP.NET Core MVC app that talks only to your Part 2 API. You then add cloud image storage with Azure Blob Storage and package the app in Docker.

What had to be done
MVC setup: The MVC app gets all its data from the Part 2 API, with no database access or copied business logic.
Authentication and navigation:
Register and log in through the API, with session management.
A different menu for each role.
Protected pages redirect unauthenticated users.
Logout ends the session.
Organiser interface:
A dashboard with events, enrolments per event and upcoming dates.
Event create, edit and delete, including a banner image upload.
Category management.
A page showing enrolments with category and status.
Results entry for finish times and positions.
Participant interface:
A home page with filters.
Event details with categories.
Enter Event with a category.
A My Enrolments page.
A My Results page showing entries like “47th out of 312 finishers”.
A profile page with a profile picture upload.
Azure Blob Storage: Added to the API, not the MVC app. Images are stored in Azure and displayed on the event page.
Docker: The MVC app runs in a container with a well-structured Dockerfile, and the README has the run command.
UI design: A sporting RaceDay theme, Bootstrap, colour-coded enrolment statuses, graceful error handling, and a consistent look.
GitHub and CI/CD: At least 20 meaningful commits, and the workflow updated to build the MVC project as well.
YouTube link:
