---
layout: post
title: "Why Is Your RAG Pipeline Slower Than It Needs to Be?"
date: 2026-09-10 22:33:39 -0400
tags: [chunk, llm, rag, latency, local, qwen38-27b]
author: the.serf
---

Most production RAG systems are not slow because the LLM is slow. They're slow because of what happens in the 200–400 milliseconds before the model ever sees a token: embedding generation, vector search, metadata filtering, context assembly, and the inevitable "wait, is this chunk even relevant?" re-query that nobody budgeted for. The model's time-to-first-token is the easy number to watch. The retrieval latency is where the real engineering debt lives.

## The Embedding Bottleneck Most People Miss

When you receive a user query, you embed it, then search. That's one embedding call. Fine. But what about the documents you're indexing? If you're re-embedding on every ingestion batch without a content hash or a "has this document changed" check, you're paying the embedding API cost for documents that haven't moved. A simple content-addressed cache (hash the document text, skip if the hash matches the stored hash) eliminates a surprising amount of redundant work, especially for semi-static corpora like internal wikis or product documentation that get re-synced on a schedule.

## Chunking Is a Latency Decision, Not Just a Quality Decision

This is the one that trips people up. The chunk size you pick in your ingestion pipeline directly determines:

- How many candidates your vector search returns
- How much context you stuff into the prompt (cost + latency)
- How much the model has to "wade through" irrelevant text before finding the answer

A 4,000-character chunk with 50% overlap means your vector search is comparing against 1.5× more vectors than a 2,000-character chunk with no overlap, and your prompt is 2× longer. If your documents have natural structural boundaries (headings, paragraphs, function definitions), using them as chunk boundaries instead of a fixed character count tends to produce more semantically coherent results *and* shorter context windows. You get better answers in fewer tokens. Win-win, except for the team that spent three weeks tuning overlap percentages.

## The Metadata Filter You Forgot to Add

Vector similarity is a blunt instrument. If your corpus contains documents from multiple products, multiple languages, or multiple time periods, and you're not filtering by metadata *before* the vector search, you're asking the ANN index to do work that a structured query could handle in microseconds. A `product = "widget-prod"` filter applied at query time shrinks the candidate set by orders of magnitude. The vector search then operates on a small, relevant subset instead of scanning the entire index. This is the difference between a 15 ms search and a 200 ms search on a mid-sized corpus, and it's the single highest-leverage optimization most teams skip because they're so focused on the embedding model choice.

## The Reranking Step Is Not Optional (Anymore)

Top-k vector retrieval gives you the k most *similar* chunks, not the k most *relevant* ones. Similarity and relevance are correlated but not identical. A lightweight reranking model (a cross-encoder that scores the query-document pair) applied to your top-50 candidates and returning the top-5 typically improves answer quality noticeably with a latency cost of 30–80 ms. If your current pipeline goes straight from vector search to prompt assembly, you're leaving accuracy on the table. The tradeoff is almost always in reranking's favor unless you're at a sub-100 ms total budget, in which case you have other problems.

## The Two Things to Profile First

If you're looking at a RAG pipeline and it feels slow, grab a trace and look at two numbers:

1. **Time from user query to first embedding call.** If this is non-trivial, you have application-level overhead (serialization, auth, routing) that's unrelated to the AI stack.
2. **Time from vector search completion to LLM request dispatch.** If there's a gap here, something in your context assembly (formatting, token counting, conditional logic) is adding latency that isn't buying you quality.

Everything else — model choice, temperature, max tokens — is tuning. These two numbers are architecture.

## The Uncomfortable Truth

The LLM is the expensive, interesting, hard-to-predict part of your system. The retrieval layer is the boring, predictable, *fixable* part. Teams tend to spend their debugging energy on prompt engineering and model selection because that's where the visible quality issues show up. But the latency budget is usually being eaten by the boring part, and the boring part is where you have the most control. Profile the retrieval path before you touch the prompt.