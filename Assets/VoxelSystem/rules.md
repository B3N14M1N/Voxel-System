# Code Refactoring Rules

## Core Principles

1. **NO CODE LOGIC MODIFICATIONS**: The functional code must remain unchanged. The refactoring applies ONLY to non-functional elements.
2. **TREAT CODE AS PERFECT**: Assume the code functions perfectly as is. Any alteration to the logic will break the software.

## Project Information

1. **Unity Version**: Unity 6
2. **Plugins Used**:
   - Zenject (Dependency Injection framework)
   - TextMesh Pro (Text rendering system)
   - Any other referenced plugins should be considered integral to the project

## Allowed Modifications

1. **Formatting**: 
   - Consistent indentation
   - Proper spacing between operators
   - Line breaks for readability
   - Consistent brace placement

2. **Comments**:
   - Remove all XML comments and replace with new XML comments
   - If the original comments had a good explaination, they can be reused
   - Keep comments short, clear, and factual
   - Comments should explain WHAT the method does, not assumptions about WHY
   - No speculative language such as "probably", "seems to be", "might be intended to"

3. **Whitespace**:
   - Consistent spacing between methods and classes
   - Logical grouping of related code blocks with whitespace
   - Remove redundant blank lines

## Prohibited Modifications

1. **Code Changes**:
   - NO modifications to method implementations
   - NO changes to variable names or types
   - NO alterations to flow control structures
   - NO "improvements" to algorithms or logic
   - NO optimization of code execution

2. **Structural Changes**:
   - NO adding or deleting any files
   - NO moving code between files
   - NO changing class hierarchies

3. **Access Modifiers**:
   - NO changes to access levels (private, public, protected, internal)
   - NO addition of readonly, const, or other modifiers
   - NO parameter modifications of any kind

4. **Plugin Interactions**:
   - NO changes to how the code interacts with Zenject or TextMesh Pro
   - NO modifications to dependency injection setup
   - Maintain all plugin-specific attributes and annotations

## XML Comment Handling

1. **Replace all XML comments** unless:
   - They contain critical API documentation needed for external integration
   - They include legal notices or licensing information
   - They contain essential implementation details not evident from the code

2. **For any retained XML comments**, provide explicit justification for why they must remain

## Comment Standards

1. **XML Method Comments**:
   - Begin with a verb describing the action
   - Describe concrete functionality, not intentions
   - No assumptions about edge cases unless explicitly handled in code

2. **XML Class Comments**:
   - Describe the concrete purpose and responsibility
   - No speculation about design patterns or architectural intentions

3. **Property Comments**:
   - Describe what the property represents, not how it might be used
   - No assumptions about valid value ranges unless enforced in code

## General Guidelines

1. Treat this as a documentation and readability exercise only
2. When in doubt, leave code unchanged rather than risk altering behavior
3. Focus on consistency across the entire codebase
4. Remember that every single character of functional code is considered critical and unchangeable
5. Be mindful of Unity 6 and plugin-specific code patterns