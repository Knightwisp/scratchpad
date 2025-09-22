# Request: Sanlam Fintech Assessment

## Exercise: Bank account withdrawal code improvement 

Improve on the provided code snippet, which outlines a basic banking operation involving an account balance withdrawal and an event notification, which is part of the core domain for the organization. You have full carte blanche to suggest improvements across any and all aspects of the implementation. Your recommendations should aim to enhance the code's **structure, efficiency, throughput, maintainability, flexibility, consistency, fault tolerance, testability, dependency management, observability, auditability, portability, correctness, cost efficiency, data governance, interoperability, architecture and overall quality and more**, while _preserving the existing business functionality_.

## Instructions
- Feel free to use a programming language you feel comfortable with. 
- Security is not part of this exercise.
- The code snippet does not need to compile and run.
- You can omit unit/integration testing for this particular piece of code.

### Expectations
- An outline of your approach, ensuring that the fundamental business capability remains unchanged.
- Elaboration on any implementation choices.
- The fixed code snippet.
- Document any unclear library usage.

# Response

## Format
Assessment feedback must maintain focus on enhancement aspects (design). Artefacts should be concise while emphasizing and enabling feedback and expression of thoughtful design experience. Responses to the exercise are given in C# ("comfortable" language) with code snippet files and markdown document(s) to walk through the outline of issues and elaborate on implementation options. 

## Artefacts
- bankWithdrawelApi_commented.java - code snippet w/ comments annotating issue points
- bankWithdrawelApi.cs - code snippet w/ comments annotating issue points translated to .Net
- [bank_withdrawal_improvement_outline.md](bank_withdrawal_improvement_outline.md) - table containing outline of approach to addressing the code snippet, and a presentable walkthrough of suggestions along enhancement aspects emphasized in the exercise. Examples here are written in C# (.Net).

### Technologies (Java)
- **Amazon SNS**: Fully managed pub/sub messaging service for decoupling distributed systems and microservices
- **Spring Framework**: Comprehensive Java application framework providing dependency injection, transaction management, and web capabilities
- **Java BigDecimal**: High-precision decimal arithmetic class designed for exact financial calculations

### Technologies (.Net)
- **AWS SDK for .NET**: Amazon Web Services SDK providing SNS client and other AWS service integrations for .NET applications
- **ASP.NET Core**: Modern, cross-platform framework for building high-performance web APIs and applications
- **System.Decimal**: .NET's built-in high-precision decimal type designed for exact financial and monetary calculations

## Stretch Goal: Functional Code
What would it take to get bankWithdrawelApi.cs executable? A container with SQL Server? A script to compile and run the C# from inside this lean project structure then hit the container?

