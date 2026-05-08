# data-ingestion

# How to run: 

# Architecture description

# Trade-offs you considered, and what you’d do differently with more time section:

1. **Why used MSSQL?** After reading and brainstormed test task(with some basic models, some interaction inside) decided to use MSSQL because i decided to make transaction structured and have chunk logic for increasing performance of my app.

2. **Private password policy.** I know that store credential in appsetting (which will be pushed to production) or anywhere in code it`s very bad approach. Better them to store in user secrets or in Google Cloud Secret Manager. But I wanted just to show how I think without sensitive data protection overhead.

3. **CI/CD** If I had more time, I consider to add CI/CD pipeline (Something with Terraform and Google Cloud Builds).

4. **I`ve use more interfaces (not only just for unit tests)** But i decided as for MVP it will be sufficient.

5. Make StatsController to have some filtering too as CustomerController (maybe some generic logic)

6. Also add logging, metrics e.g. Prometheus/Grafana with alerting, some health check events

7. More deep down into optimization of most high-loaded methods.



# **“AI Usage” section:**

Which tools did you use and for what?

For this project I used Cursor with GPT 5.5 and Sonnet 4.6.

What did you accept as-is, modify, or write from scratch?

Most of unit tests was accepted as-is after review, just configure what library and what nuggets to use.
Some modifying was done in AppDbContext.
From scratch was done some IngestionLogic.

Did the AI get anything wrong? How did you catch it?

Some unit-tests which i wanted to see needed to deeper explanation for AI tools. 
Also there was some migration failure which looped AI agent and i manually adjusted some objects properties and migrations how it should be like.
