from http import HTTPStatus
from typing import Any, cast
from urllib.parse import quote

import httpx

from ...client import AuthenticatedClient, Client
from ...types import Response, UNSET
from ... import errors

from ...models.graph_extract_request import GraphExtractRequest
from ...models.graph_extraction import GraphExtraction
from ...models.problem_details import ProblemDetails
from typing import cast



def _get_kwargs(
    id: str,
    *,
    body: GraphExtractRequest,

) -> dict[str, Any]:
    headers: dict[str, Any] = {}


    

    

    _kwargs: dict[str, Any] = {
        "method": "post",
        "url": "/api/v1/graphs/{id}/compounds".format(id=quote(str(id), safe=""),),
    }

    _kwargs["json"] = body.to_dict()

    headers["Content-Type"] = "application/json"

    _kwargs["headers"] = headers
    return _kwargs



def _parse_response(*, client: AuthenticatedClient | Client, response: httpx.Response) -> GraphExtraction | ProblemDetails | None:
    if response.status_code == 200:
        response_200 = GraphExtraction.from_dict(response.json())



        return response_200

    if response.status_code == 404:
        response_404 = ProblemDetails.from_dict(response.json())



        return response_404

    if response.status_code == 422:
        response_422 = ProblemDetails.from_dict(response.json())



        return response_422

    if client.raise_on_unexpected_status:
        raise errors.UnexpectedStatus(response.status_code, response.content)
    else:
        return None


def _build_response(*, client: AuthenticatedClient | Client, response: httpx.Response) -> Response[GraphExtraction | ProblemDetails]:
    return Response(
        status_code=HTTPStatus(response.status_code),
        content=response.content,
        headers=response.headers,
        parsed=_parse_response(client=client, response=response),
    )


def sync_detailed(
    id: str,
    *,
    client: AuthenticatedClient | Client,
    body: GraphExtractRequest,

) -> Response[GraphExtraction | ProblemDetails]:
    """ Saves some nodes as a new compound beside the graph and holds it in their place, as one undo step.

    Args:
        id (str):
        body (GraphExtractRequest): Moves some nodes into a new compound beside the graph and puts
            a node holding it in their place.

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        Response[GraphExtraction | ProblemDetails]
     """


    kwargs = _get_kwargs(
        id=id,
body=body,

    )

    response = client.get_httpx_client().request(
        **kwargs,
    )

    return _build_response(client=client, response=response)

def sync(
    id: str,
    *,
    client: AuthenticatedClient | Client,
    body: GraphExtractRequest,

) -> GraphExtraction | ProblemDetails | None:
    """ Saves some nodes as a new compound beside the graph and holds it in their place, as one undo step.

    Args:
        id (str):
        body (GraphExtractRequest): Moves some nodes into a new compound beside the graph and puts
            a node holding it in their place.

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        GraphExtraction | ProblemDetails
     """


    return sync_detailed(
        id=id,
client=client,
body=body,

    ).parsed

async def asyncio_detailed(
    id: str,
    *,
    client: AuthenticatedClient | Client,
    body: GraphExtractRequest,

) -> Response[GraphExtraction | ProblemDetails]:
    """ Saves some nodes as a new compound beside the graph and holds it in their place, as one undo step.

    Args:
        id (str):
        body (GraphExtractRequest): Moves some nodes into a new compound beside the graph and puts
            a node holding it in their place.

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        Response[GraphExtraction | ProblemDetails]
     """


    kwargs = _get_kwargs(
        id=id,
body=body,

    )

    response = await client.get_async_httpx_client().request(
        **kwargs
    )

    return _build_response(client=client, response=response)

async def asyncio(
    id: str,
    *,
    client: AuthenticatedClient | Client,
    body: GraphExtractRequest,

) -> GraphExtraction | ProblemDetails | None:
    """ Saves some nodes as a new compound beside the graph and holds it in their place, as one undo step.

    Args:
        id (str):
        body (GraphExtractRequest): Moves some nodes into a new compound beside the graph and puts
            a node holding it in their place.

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        GraphExtraction | ProblemDetails
     """


    return (await asyncio_detailed(
        id=id,
client=client,
body=body,

    )).parsed
