from http import HTTPStatus
from typing import Any, cast
from urllib.parse import quote

import httpx2

from ...client import AuthenticatedClient, Client
from ...types import Response, UNSET
from ... import errors

from ...models.graph_command_batch import GraphCommandBatch
from ...models.graph_edit_result import GraphEditResult
from ...models.problem_details import ProblemDetails
from typing import cast



def _get_kwargs(
    id: str,
    *,
    body: GraphCommandBatch,

) -> dict[str, Any]:
    headers: dict[str, Any] = {}


    

    

    _kwargs: dict[str, Any] = {
        "method": "post",
        "url": "/api/v1/graphs/{id}/commands".format(id=quote(str(id), safe=""),),
    }

    _kwargs["json"] = body.to_dict()

    headers["Content-Type"] = "application/json"

    _kwargs["headers"] = headers
    return _kwargs



def _parse_response(*, client: AuthenticatedClient | Client, response: httpx2.Response) -> GraphEditResult | ProblemDetails | None:
    if response.status_code == 200:
        response_200 = GraphEditResult.from_dict(response.json())



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


def _build_response(*, client: AuthenticatedClient | Client, response: httpx2.Response) -> Response[GraphEditResult | ProblemDetails]:
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
    body: GraphCommandBatch,

) -> Response[GraphEditResult | ProblemDetails]:
    """ Applies a batch of edits, whole or not at all, as one undo step.

    Args:
        id (str):
        body (GraphCommandBatch): Some edits, applied whole or not at all, and undone as one step.

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx2.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        Response[GraphEditResult | ProblemDetails]
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
    body: GraphCommandBatch,

) -> GraphEditResult | ProblemDetails | None:
    """ Applies a batch of edits, whole or not at all, as one undo step.

    Args:
        id (str):
        body (GraphCommandBatch): Some edits, applied whole or not at all, and undone as one step.

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx2.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        GraphEditResult | ProblemDetails
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
    body: GraphCommandBatch,

) -> Response[GraphEditResult | ProblemDetails]:
    """ Applies a batch of edits, whole or not at all, as one undo step.

    Args:
        id (str):
        body (GraphCommandBatch): Some edits, applied whole or not at all, and undone as one step.

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx2.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        Response[GraphEditResult | ProblemDetails]
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
    body: GraphCommandBatch,

) -> GraphEditResult | ProblemDetails | None:
    """ Applies a batch of edits, whole or not at all, as one undo step.

    Args:
        id (str):
        body (GraphCommandBatch): Some edits, applied whole or not at all, and undone as one step.

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx2.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        GraphEditResult | ProblemDetails
     """


    return (await asyncio_detailed(
        id=id,
client=client,
body=body,

    )).parsed
