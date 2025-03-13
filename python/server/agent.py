# agent.py
#
# Handles the ChatOpenAI LLM initialization and communication with
# conversational memory so the agent knows what was said before.
#
# The logic is:
# 1) We keep a conversation history of user/agent messages.
# 2) The LLM now returns only two keys in JSON: "conversation_answer" and "media_urls".
# 3) "conversation_answer" is the main text response we want to speak and display.

import json
from typing import List, Dict
from langchain.schema import HumanMessage, AIMessage
from langchain_community.chat_models import ChatOpenAI

class Agent:
    def __init__(self, openai_api_key: str, model_name: str = "gpt-4o-mini", temperature: float = 0.7):
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

    def add_agent_message(self, conversation_answer: str):
        """
        Adds an agent message to the conversation history.
        """
        self.conversation_history.append({
            "speaker": "agent",
            "text": conversation_answer
        })

    def run(self, user_message: str, context_str: str) -> str:
        """
        Constructs a conversation thread where:
         - Past user messages are 'HumanMessage'
         - Past agent messages are 'AIMessage'
         - The final user message is appended with Pinecone context
           as the last 'HumanMessage' to which the LLM replies.

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
You are talking to the user as Michal would, in first person on Michal's behalf. Speak in a very conversational tone with very short responses. Include concrete details. Vary the responses between short and very short. Don't talk too much, less is more.

User question:
{user_message}

Relevant context:
{context_str}

Generate a STRICT JSON object with the keys "conversation_answer" and "media_urls". Example:
{{
  "conversation_answer": "...",
  "media_urls": ["...", "..."]
}}

Where:
- "conversation_answer" is your main text response.
- "media_urls" is a list of any relevant URLs from the context, or an empty list if none apply.

Output ONLY valid JSON with NO additional commentary.
"""
        messages.append(HumanMessage(content=prompt))

        # 3) Call the LLM and return the raw JSON string
        try:
            llm_response = self.llm.predict_messages(messages)
            return llm_response.content
        except Exception as e:
            print("Error calling LLM:", e)
            return None
