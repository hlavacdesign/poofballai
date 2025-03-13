# agent.py
#
# Handles the ChatOpenAI LLM initialization and communication.

import json
from langchain_community.chat_models import ChatOpenAI
from langchain.schema import HumanMessage

class Agent:
    def __init__(self, openai_api_key: str, model_name: str = "gpt-4o-mini", temperature: float = 0.7):
        self.llm = ChatOpenAI(
            openai_api_key=openai_api_key,
            model_name=model_name,
            temperature=temperature
        )

    def run(self, user_message: str, context_str: str) -> str:
        """
        Constructs the prompt (HumanMessage) and sends it to the LLM,
        returning the raw JSON response as a string.
        """
        prompt = f"""
You are Version One, a virtual representation of Michal Hlavac. 
You are talking to the user as Michal may speak in first person on Michal's behalf.

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

Note: If any URLs in the context are relevant, please include them in "media_urls". 
Output ONLY valid JSON, with no extra commentary.
"""
        messages = [HumanMessage(content=prompt)]
        try:
            llm_response = self.llm.predict_messages(messages)
            return llm_response.content
        except Exception as e:
            print("Error calling LLM:", e)
            return None
