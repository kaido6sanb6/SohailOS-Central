# Data layer

This project owns persistent local application data. SQLite/EF Core can be introduced here without coupling the domain layer to a database implementation.

Secrets and personal sensitive data must not be committed to the repository.
