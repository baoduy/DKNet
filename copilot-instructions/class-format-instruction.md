# Class Formatting and Documentation Best Practices Instruction

## 1. Use primary construction of .NET 9 if available.
- Convert the class constructor into Class primary constructor if possible to simplify the code.
  
## 2. Private Members Naming Convention
- Prefix all private members with an underscore (e.g., `_memberName`) to clearly distinguish them from public members.

## 3. Method Organization
- Group methods logically (e.g., by functionality or access level) to improve readability and ease of navigation.

---

## Class XML Documentation
For every class, provide comprehensive XML documentation in the `<summary>` tag covering:

### 1. Purpose and Rationale
- **Purpose:** Explain why the class was created.
- **Rationale:** Describe the problem or challenge the class addresses.

### 2. Functionality and Role
- **Functionality:** Summarize the main responsibilities of the class.
- **Integration:** Explain how the class fits into the overall solution.

### 3. Usage Best Practices
- **Recommendations:** Offer guidelines for using the class effectively.
- **Considerations:** Highlight any special practices to ensure optimal and maintainable usage.

### 4. Additional Considerations
- **Consistency:** Maintain a clear, consistent tone throughout.
- **Regular Updates:** Keep documentation current as the code evolves.
- **Readability:** Format comments for clarity in IntelliSense and other IDE tools.

---

## Method XML Documentation
For every public method, include XML documentation that covers:

### Summary and Parameters
- **Summary:** Describe the method’s functionality using the `<summary>` tag.
- **Parameters:** Use `<param>` tags for each parameter.
- **Return Value:** Include a `<returns>` tag if the method returns a value.

### Exception Handling
- **Exceptions:** Document any exceptions the method may throw using `<exception>` tags.

### Additional Notes
- **Remarks:** Use the `<remarks>` tag to provide extra details, such as usage scenarios or performance considerations.
- **Cross-Referencing:** Utilize `<see cref="..."/>` or `<seealso cref="..."/>` tags to reference related classes or methods.

---

## Property XML Documentation
For every public property, include XML documentation that covers:

### Summary and Value
- **Summary:** Use the `<summary>` tag to explain the purpose of the property.
- **Value:** Use the `<value>` tag to describe what the property represents and any key characteristics.

### Access and Behavior
- **Access Notes:** Indicate whether the property is read-only, write-only, or read-write.
- **Behavior:** If applicable, note any special behavior, side effects, or constraints related to getting or setting the property value.

### Additional Considerations
- **Remarks:** Provide extra context or usage notes with the `<remarks>` tag when needed.
- **Examples:** If useful, include brief usage examples to clarify how the property should be used.
  
> **Note:** High-quality documentation is essential for code maintainability, effective onboarding, and long-term clarity. Clear and consistent documentation benefits both current and future developers.
