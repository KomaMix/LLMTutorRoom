export function FeedbackSections({ sections, variant = "student" }) {
  const visibleSections = (Array.isArray(sections) ? sections : [])
    .map(section => ({
      ...section,
      text: typeof section.text === "string" ? section.text.trim() : "",
      items: (Array.isArray(section.items) ? section.items : [])
        .filter(item => typeof item === "string")
        .map(item => item.trim())
        .filter(Boolean)
    }))
    .filter(section => section.text || section.items.length > 0);

  if (visibleSections.length === 0) {
    return null;
  }

  return (
    <div className={`structured-feedback ${variant}`}>
      {visibleSections.map(section => (
        <section key={section.sectionId}>
          <h5>{section.title}</h5>
          {section.format === "list" ? (
            <ul>
              {section.items.map((item, index) => (
                <li key={`${section.sectionId}-${index}`}>{item}</li>
              ))}
            </ul>
          ) : (
            <p>{section.text}</p>
          )}
        </section>
      ))}
    </div>
  );
}
