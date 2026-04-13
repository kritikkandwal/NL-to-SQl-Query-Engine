Natural Language to SQL Query Engine

Overview

This project is a backend system that converts natural language queries into SQL queries, allowing non-technical users to interact with databases without writing SQL.

Instead of writing queries like:

```
SELECT name FROM employee WHERE salary > 50000;
```

A user can simply ask:

👉 "Show employees with salary greater than 50000"

And the system will generate and execute the SQL query.


Problem Statement

Most real-world databases require knowledge of SQL to extract data.

But:

* Non-technical users (HR, managers, analysts) cannot write SQL
* Even developers need time to understand schema before querying
* Manual query writing is error-prone

There is a need for a system that:

* Understands natural language
* Converts it into correct SQL
* Ensures safe query execution


Solution

This project solves the problem by building a **schema-aware NL → SQL engine** that:

* Parses natural language input
* Identifies tables, columns, and conditions
* Generates valid SQL queries
* Executes them safely on the database


What Makes This Project Different?

This is not just keyword matching.

Key highlights:

* Schema-aware parsing
  Understands database structure before generating queries

* Automatic JOIN detection
  Identifies relationships between tables

* Condition extraction
  Handles filters like:

  * greater than
  * less than
  * equal to

* Multi-language support (basic)
  Supports English and Italian keywords

* Safe query execution
  Only allows SELECT queries (no DELETE/UPDATE)


How It Works (Step-by-Step)

1. User inputs a natural language query
   Example:
   👉 "Show all employees in department IT"

2. Language Processing:

   * Identify operation (SELECT, COUNT, etc.)
   * Extract keywords and intent

3. Schema Mapping:

   * Match words to table names
   * Match words to column names

4. Condition Extraction:

   * Detect filters (e.g., salary > 50000)

5. SQL Generation:

   * Construct SQL query

6. Validation Layer:

   * Ensure query is safe (SELECT only)

7. Execution:

   * Run query on database
   * Return results


System Architecture

* API Layer → Flask / .NET Controllers
* Processing Layer → NLP parsing logic
* Schema Layer → Database structure extraction
* Query Builder → SQL generation engine
* Execution Layer → Safe query execution


Key Features

1) Natural Language Querying

* Convert English sentences into SQL

2) JOIN Handling

* Automatically join related tables

3) Aggregations

Supports:

* COUNT
* MAX
* MIN
* AVG
* SUM

4) Multi-language Support

* English + Italian keywords

5) Security Layer

* Restricts queries to SELECT only
* Prevents harmful SQL execution


Evaluation

To test the system, a small dataset of natural language queries was created.

Test Coverage:

* Simple SELECT queries
* Filter conditions
* Aggregations
* JOIN queries
* Different wording patterns

Results:

* Tested on ~30 queries
* ~27 queries produced correct SQL and results
* Few failures due to ambiguous phrasing

Accuracy:

 ~90% correctness in controlled test cases


Tech Stack

* Backend: Python, Flask, .NET MVC (API layer)
* Database: MySQL / SQLite
* Core Logic: Rule-based NLP parsing
* APIs: REST APIs


Limitations

* Does not handle highly complex nested queries
* Struggles with ambiguous natural language
* Rule-based system (not deep learning / LLM-based)


Future Improvements

* Integrate LLM (like GPT or T5) for better understanding
* Improve ambiguity handling
* Add query explanation (why this SQL was generated)
* Support more languages


Conclusion

This project demonstrates how natural language can be translated into structured database queries.

It bridges the gap between:
👉 Human language
➡️ Database systems

While keeping the system safe, explainable, and practical.


Author

Kritik Kandwal
B.Tech CSE (3rd Year)
