I would like to build a suspense system in QuickMarkup.

Concept:
```
wait {
    <UserProfile />
    <Weather />
}
loading (state) {
    <ProgressRing />
}
failed (state) {
    <ErrorView Exception=`state.FirstFailure.Exception` />
} 
```

Inspired by React's Suspense:
```tsx
<Suspense fallback={<Spinner/>}>
    <UserProfile/>
</Suspense> 
```

Now, we're not going to build the language. We're building the infrastructure required.

So, the primary goal is that, we will have to build this block that will register itself to the context as a suspense handler, and we will need the infrastructure that will help us in building this language.

We will nee this for both a single child and multi-child version.

Recommendation:
- It will probably be a IUIBlock<TElement> to be able to integrate with the rest of the system. I'm not sure how single child is handled in if/else right now but probably a similar system will be needed for a single child suspense.
- The state variable will be an observable list of Task or some kind of task representation.
- While we are not wiring this in yet. In the future, we plan for async components to do some preprocessing, such as looking into this context and tell the context that we are a new component.