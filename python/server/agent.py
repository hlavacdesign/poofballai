# agent.py
#
# Handles the ChatOpenAI LLM initialization and communication with
# conversational memory so the agent knows what was said before.
#
# The logic is:
# 1) The most recent user message (with Pinecone context) is always sent in full
#    as the last message.
# 2) Before that, we include the conversation history:
#    - "user" entries contain the actual user text (no URLs)
#    - "agent" entries contain the agent's short_response from prior turns

import json
from typing import List, Dict
from langchain.schema import HumanMessage, AIMessage
from langchain_community.chat_models import ChatOpenAI

class Agent:
    def __init__(self, openai_api_key: str, model_name: str = "gpt-4.5-preview-2025-02-27", temperature: float = 0.7):
        self.llm = ChatOpenAI(
            openai_api_key=openai_api_key,
            model_name=model_name,
            temperature=temperature
        )
        # We'll store conversation as a list of dicts, e.g. {"speaker": "user"/"agent", "text": "..."}
        self.conversation_history: List[Dict[str, str]] = []

    def add_user_message(self, user_message: str):
        """
        Adds a user message to the conversation history.
        (Exclude any URLs or extraneous info here, just the actual text.)
        """
        self.conversation_history.append({
            "speaker": "user",
            "text": user_message
        })

    def add_agent_message(self, short_response: str):
        """
        Adds an agent message to the conversation history.
        (We record only the short_response, no URLs.)
        """
        self.conversation_history.append({
            "speaker": "agent",
            "text": short_response
        })

    def run(self, user_message: str, context_str: str) -> str:
        """
        Constructs a conversation thread where:
         - Past user messages are 'HumanMessage'
         - Past agent short_responses are 'AIMessage'
         - The final user message is appended with the Pinecone context
           and becomes the last 'HumanMessage' to which the LLM replies.

        Returns the LLM's raw JSON string (or None on error).
        """
        # 1) Build up the prior conversation
        messages = []
        for entry in self.conversation_history:
            if entry["speaker"] == "user":
                messages.append(HumanMessage(content=entry["text"]))
            else:  # "agent"
                messages.append(AIMessage(content=entry["text"]))

        # 2) Append the *latest* user message along with any Pinecone context
        prompt = f"""
You are Version One, a virtual representation of Michal Hlavac. 
You are talking to the user as Michal would, in first person on Michal's behalf.

User question:
{user_message}

Relevant context:
{context_str}

Generate a STRICT JSON object with the keys "long_answer", "short_answer", and "media_urls". Example:
{{
  "long_answer": "...",
  "short_answer": "...",
  "media_urls": ["...", "..."]
}}

Where:
- long_answer is a very short response to the user, using the context if relevant
- short_answer is a concise summary
- media_urls is a list of one or more URLs IF relevant, otherwise an empty list.

If any URLs in the context seem relevant, include them in "media_urls".
Output ONLY valid JSON, with no extra commentary.
"""
        messages.append(HumanMessage(content=prompt))

        # 3) Call the LLM and return the raw JSON string
        try:
            llm_response = self.llm.predict_messages(messages)
            return llm_response.content
        except Exception as e:
            print("Error calling LLM:", e)
            return None
